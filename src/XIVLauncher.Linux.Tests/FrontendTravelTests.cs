using System.Net;
using System.Text.Json;
using XIVLauncher.DCTravel;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Linux.Frontend;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class FrontendTravelTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "frontend-travel-" + Guid.NewGuid());
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    private FrontendTravel Service(Transport transport) => new(root, () => new ChinaTravelSession(new DCTravelClient("private-cookie", transport)),
        (_, _, _) => Task.CompletedTask, () => { });
    private static async Task<OperationView> Run(FrontendTravel service, FrontendOperations operations, TravelRequest request)
    {
        var identity = JsonSerializer.SerializeToElement(service.Start(request, operations)).GetProperty("operationId").GetString()!;
        for (var i = 0; i < 300 && operations.Busy; i++) await Task.Delay(10);
        var result = operations.Poll(new(identity)); Assert.False(operations.Busy); return result;
    }
    private static TravelView Data(OperationView result) { Assert.Equal("completed", result.State); return Assert.IsType<TravelView>(result.Result); }

    [Fact] public async Task TravelUsesServerChoicesAndPersistsAreaOnlyAfterSuccess()
    {
        using var transport = new Transport(); using var service = Service(transport); await using var operations = new FrontendOperations(root);
        var view = Data(await Run(service, operations, new("open", Account: "Synthetic")));
        Assert.Single(view.Characters); Assert.Single(view.Orders);
        var json = JsonSerializer.Serialize(view);
        Assert.DoesNotContain("private-cookie", json); Assert.DoesNotContain("private-role-id", json); Assert.DoesNotContain("private-order-id", json);
        var tcFile = Path.Combine(root, "ffxiv_tc/settings.json"); var beforeTc = File.ReadAllBytes(tcFile);
        var done = Data(await Run(service, operations, new("transfer", Session: view.Session, Revision: view.Revision, Target: view.Targets[0].Groups[0].Id)));
        Assert.Equal("Destination", done.CompletedArea); Assert.Equal("Destination", LinuxSettings.Load(root).AreaName);
        Assert.Equal(1, transport.Submissions); Assert.Empty(done.Characters);
        Assert.Equal(beforeTc, File.ReadAllBytes(tcFile));
        Assert.Throws<ArgumentException>(() => service.Start(new("transfer", Session: view.Session, Revision: view.Revision, Target: "2:20"), operations));
    }

    [Fact] public async Task AmbiguousSubmissionIsNotReplayedAndCanRefreshHistory()
    {
        using var transport = new Transport { FailSubmission = true }; using var service = Service(transport); await using var operations = new FrontendOperations(root);
        var view = Data(await Run(service, operations, new("open", Account: "Synthetic")));
        var request = new TravelRequest("transfer", Session: view.Session, Revision: view.Revision, Target: view.Targets[0].Groups[0].Id);
        var failed = await Run(service, operations, request);
        Assert.Equal("failed", failed.State); Assert.Equal(1, transport.Submissions); Assert.NotEqual("Destination", LinuxSettings.Load(root).AreaName);
        Assert.Throws<ArgumentException>(() => service.Start(request, operations));
        Assert.Equal(view.Session, Data(await Run(service, operations, new("history", Session: view.Session, Page: 1))).Session);
        Assert.Equal(1, transport.Submissions);
    }

    [Fact] public async Task CancellationAndWrongSessionDoNotChangeAreaOrSubmitAgain()
    {
        using var transport = new Transport { WaitForCancellation = true }; using var service = Service(transport); await using var operations = new FrontendOperations(root);
        var view = Data(await Run(service, operations, new("open", Account: "Synthetic")));
        Assert.Throws<ArgumentException>(() => service.Start(new("history", Session: "wrong", Page: 1), operations));
        var identity = JsonSerializer.SerializeToElement(service.Start(new("transfer", Session: view.Session, Revision: view.Revision, Target: "2:20"), operations)).GetProperty("operationId").GetString()!;
        await transport.PollStarted.Task.WaitAsync(TimeSpan.FromSeconds(5)); operations.Cancel(new(identity));
        for (var i = 0; i < 100 && operations.Busy; i++) await Task.Delay(10);
        Assert.Equal("cancelled", operations.Poll(new(identity)).State); Assert.Equal(1, transport.Submissions);
        Assert.NotEqual("Destination", LinuxSettings.Load(root).AreaName);
        Data(await Run(service, operations, new("history", Session: view.Session, Page: 1)));
        service.Dispose(); Assert.Throws<ArgumentException>(() => service.Start(new("history", Session: view.Session, Page: 1), operations));
    }

    [Fact] public async Task ReturnUsesSelectedCurrentWorldAndOriginalOrder()
    {
        using var transport = new Transport(); using var service = Service(transport); await using var operations = new FrontendOperations(root);
        var view = Data(await Run(service, operations, new("open", Account: "Synthetic")));
        var done = Data(await Run(service, operations, new("return", Session: view.Session, Revision: view.Revision, Order: view.Orders[0].Id, Current: "2:20")));
        Assert.Equal("Source", done.CompletedArea); Assert.Contains("travelOrderId=private-order-id", transport.LastSubmission);
        Assert.Contains("groupId=20", transport.LastSubmission); Assert.Equal("Source", LinuxSettings.Load(root).AreaName);
    }

    private sealed class Transport : HttpMessageHandler
    {
        internal bool FailSubmission, WaitForCancellation;
        internal int Submissions;
        internal string LastSubmission = "";
        internal TaskCompletionSource PollStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var action = request.RequestUri!.AbsolutePath.Split('/').Last(); object data;
            var areas = new[] {
                new { areaId = 1, areaName = "Source", groups = new[] { new { groupId = 10, groupName = "Source world", groupCode = "S" } } },
                new { areaId = 2, areaName = "Destination", groups = new[] { new { groupId = 20, groupName = "Destination world", groupCode = "D" } } }
            };
            switch (action)
            {
                case "pageInit": data = new { resultCode = 0 }; break;
                case "queryGroupListTravelSource": data = new { resultCode = 0, groupList = JsonSerializer.Serialize(areas) }; break;
                case "queryGroupListTravelTarget": data = new { resultCode = 0, groupList = JsonSerializer.Serialize(areas.Skip(1)) }; break;
                case "queryRoleList4Migration": data = new { resultCode = 0, roleList = "[{\"roleId\":\"private-role-id\",\"roleName\":\"Synthetic Role\"}]" }; break;
                case "queryMigrationOrders": data = new { resultCode = 0, totalCount = 1, totalPageNum = 1,
                    orderlist = "[{\"orderId\":\"private-order-id\",\"migrationType\":4,\"migrationStatus\":5,\"travelStatus\":1,\"groupId\":10,\"groupCode\":\"S\",\"groupName\":\"Source world\",\"areaName\":\"Source\",\"targetAreaName\":\"Destination\",\"targetGroupName\":\"Destination world\",\"createTime\":\"2026-10-09\",\"migrationDetailList\":[{\"roleId\":\"private-role-id\",\"roleName\":\"Synthetic Role\"}]}]" }; break;
                case "travelOrder": case "travelBack":
                    Submissions++; LastSubmission = request.RequestUri.Query;
                    if (FailSubmission) throw new HttpRequestException("Synthetic ambiguous response.");
                    data = new { resultCode = 0, orderId = "submitted-order" }; break;
                case "queryOrderStatus":
                    PollStarted.TrySetResult(); if (WaitForCancellation) await Task.Delay(Timeout.Infinite, token);
                    data = new { migrationStatus = 5 }; break;
                default: throw new InvalidOperationException("Unexpected endpoint: " + action);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { return_code = 0, data })) };
        }
    }
}
