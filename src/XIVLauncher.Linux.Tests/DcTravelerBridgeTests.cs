using System.Net;
using System.Text;
using System.Text.Json;
using XIVLauncher.DCTravel;
using XIVLauncher.Linux.ChinaTravel;
using XIVLauncher.Login.Models;
using XIVLauncher.Login.Workflow;
using Xunit;

namespace XIVLauncher.Linux.Tests;

public sealed class DcTravelerBridgeTests
{
    private static Task<HttpResponseMessage> Post(HttpClient http, int port, string json) =>
        http.PostAsync($"http://127.0.0.1:{port}/dctravel/", new StringContent(json, Encoding.UTF8, "application/json"));

    [Fact]
    public async Task PluginEnvelopeAndCaseSensitiveResponseWorkOverHttp()
    {
        await using var bridge = new DcTravelerBridge((method, args) =>
        {
            Assert.Equal("QueryGroupListTravelTarget", method);
            Assert.Equal(9, args[0].GetInt32()); Assert.Equal(5, args[1].GetInt32());
            return Task.FromResult<object?>(new[] { new DCTravelArea { AreaID = 9, AreaName = "Test", GroupList =
                [new DCTravelGroup { AreaID = 9, AreaName = "Test", GroupID = 5, GroupName = "World", GroupCode = "code" }] } });
        });
        using var http = new HttpClient();
        using var response = await Post(http, bridge.Port, """{"Method":"QueryGroupListTravelTarget","Params":[9,5]}""");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("Error").ValueKind);
        var area = json.RootElement.GetProperty("Result")[0];
        Assert.Equal(9, area.GetProperty("areaId").GetInt32());
        Assert.Equal(9, area.GetProperty("groups")[0].GetProperty("AreaId").GetInt32());
        Assert.Equal(5, area.GetProperty("groups")[0].GetProperty("groupId").GetInt32());
    }

    [Fact]
    public async Task RefreshAndAreaSelectionDispatchWithoutCallingTravelWebsite()
    {
        string? selected = null;
        var refresh = new LoginSessionRefreshContext
        {
            RefreshGameSessionIdAsync = () => Task.FromResult("test-session"),
            RefreshDcTravelSessionIdAsync = () => throw new Exception("Must not call website")
        };
        await using var bridge = new DcTravelerBridge(refresh, [new LoginArea { AreaName = "Test" }], name => selected = name);
        using var http = new HttpClient();
        using var response = await Post(http, bridge.Port, """{"Method":"RefreshGameSessionId","Params":[]}""");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("test-session", json.RootElement.GetProperty("Result").GetString());
        using var area = await Post(http, bridge.Port, """{"Method":"SetSdoArea","Params":["Test"]}""");
        Assert.Equal("Test", selected);
        using var bad = await Post(http, bridge.Port, """{"Method":"SetSdoArea","Params":["Unknown"]}""");
        Assert.Equal("Test", selected);
        Assert.Contains("Invalid DCTravelerX request", await bad.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidAndBrowserRequestsDoNotBreakServerOrInvokeActions()
    {
        var calls = 0;
        await using var bridge = new DcTravelerBridge((_, _) => { calls++; throw new Exception("secret-ticket"); });
        using var http = new HttpClient();
        using var malformed = await Post(http, bridge.Port, "{}");
        Assert.Contains("Error", await malformed.Content.ReadAsStringAsync());
        http.DefaultRequestHeaders.Add("Origin", "https://example.com");
        using var browser = await Post(http, bridge.Port, """{"Method":"TravelOrder","Params":[]}""");
        Assert.Equal(HttpStatusCode.Forbidden, browser.StatusCode);
        Assert.Equal(0, calls);
        http.DefaultRequestHeaders.Remove("Origin");
        using var failure = await Post(http, bridge.Port, """{"Method":"TravelOrder","Params":[]}""");
        Assert.DoesNotContain("secret-ticket", await failure.Content.ReadAsStringAsync());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void LobbyHostsAndIncomingModelsMatchPluginSpelling()
    {
        var data = DcTravelerBridge.SerializeLobbyHosts([new LoginArea { AreaID = "1", AreaGM = "gm", AreaLobby = "lobby" }]);
        using var json = JsonDocument.Parse(Convert.FromBase64String(data));
        Assert.Equal("gm", json.RootElement[0].GetProperty("AreaGm").GetString());
        Assert.Equal("1", json.RootElement[0].GetProperty("Areaid").GetString());
        var group = JsonSerializer.Deserialize<DCTravelGroup>("""{"AreaId":9,"groupId":5,"AreaName":"Test"}""", DcTravelerBridge.WireOptions)!;
        var character = JsonSerializer.Deserialize<DCTravelCharacter>("""{"AreaId":9,"GroupId":5,"roleId":"123","roleName":"Name"}""", DcTravelerBridge.WireOptions)!;
        Assert.Equal(9, group.AreaID); Assert.Equal(5, group.GroupID);
        Assert.Equal(9, character.AreaID); Assert.Equal(5, character.GroupID); Assert.Equal("123", character.ContentID);
    }

    [Fact]
    public async Task AllTravelMethodsReachExpectedEndpointsAndPreservePluginFields()
    {
        using var transport = new TravelTransport();
        await using var bridge = new DcTravelerBridge(null, transport: transport);
        using var http = new HttpClient();
        async Task<JsonElement> Call(string method, string parameters)
        {
            using var response = await Post(http, bridge.Port, "{\"Method\":\"" + method + "\",\"Params\":" + parameters + "}");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("Error").ValueKind);
            return json.RootElement.GetProperty("Result").Clone();
        }
        await Call("QueryGroupListTravelSource", "[]");
        await Call("QueryGroupListTravelTarget", "[9,5]");
        var roles = await Call("QueryRoleList", "[9,5]");
        Assert.Equal(9, roles[0].GetProperty("AreaId").GetInt32());
        Assert.Equal(5, roles[0].GetProperty("GroupId").GetInt32());
        await Call("QueryMigrationOrders", "[1]");
        Assert.Equal("order", (await Call("TravelOrder", """[{"AreaId":2,"AreaName":"Target","groupId":20,"groupCode":"T","groupName":"Target world"},{"AreaId":9,"AreaName":"Source","groupId":5,"groupCode":"S","groupName":"Source world"},{"roleId":"123","roleName":"Name"}]""")).GetString());
        Assert.Contains("areaId=9", transport.LastSubmission);
        Assert.Contains("targetArea=2", transport.LastSubmission);
        Assert.Contains("123", transport.LastSubmission);
        Assert.Equal(5, (await Call("QueryOrderStatus", "[\"order\"]")).GetProperty("Status").GetInt32());
        await Call("MigrationConfirmOrder", "[\"order\",true]");
        Assert.Equal("order", (await Call("TravelBack", "[\"order\",20,\"T\",\"Target world\"]")).GetString());
        Assert.Equal(2, transport.Submissions);
        transport.FailSubmission = true;
        using var failure = await Post(http, bridge.Port, """{"Method":"TravelBack","Params":["order",20,"T","Target world"]}""");
        var text = await failure.Content.ReadAsStringAsync();
        Assert.DoesNotContain("传送失败", JsonDocument.Parse(text).RootElement.GetProperty("Error").GetString());
        Assert.Equal(3, transport.Submissions); // An ambiguous mutation is never replayed.
        var before = transport.Calls;
        using var invalid = await Post(http, bridge.Port, """{"Method":"GetValidCookie","Params":[]}""");
        using var wrongCount = await Post(http, bridge.Port, """{"Method":"TravelBack","Params":[]}""");
        Assert.Equal(before, transport.Calls);
    }

    [Fact]
    public void OnlyExplicitBusyErrorsEnablePluginRetry()
    {
        Assert.Contains("繁忙", DcTravelerBridge.FormatError(new DCTravelAPIException("busy", -10600032)));
        Assert.DoesNotContain("传送失败", DcTravelerBridge.FormatError(new HttpRequestException("secret")));
    }

    private sealed class TravelTransport : HttpMessageHandler
    {
        internal int Calls, Submissions;
        internal bool FailSubmission;
        internal string LastSubmission = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            var method = request.RequestUri!.AbsolutePath.Split('/').Last();
            object data;
            switch (method)
            {
                case "pageInit": data = new { resultCode = 0 }; break;
                case "queryGroupListTravelSource": case "queryGroupListTravelTarget":
                    data = new { resultCode = 0, groupList = "[]" }; break;
                case "queryRoleList4Migration": data = new { resultCode = 0, roleList = "[{\"roleId\":\"123\",\"roleName\":\"Name\"}]" }; break;
                case "queryMigrationOrders": data = new { resultCode = 0, totalCount = 0, totalPageNum = 1, orderlist = "[]" }; break;
                case "queryOrderStatus": data = new { migrationStatus = 5 }; break;
                case "migrationConfirmOrder": data = new { resultCode = 0 }; break;
                case "travelOrder": case "travelBack":
                    Submissions++; LastSubmission = request.RequestUri.Query;
                    if (FailSubmission) throw new HttpRequestException("Ambiguous submission");
                    data = new { resultCode = 0, orderId = "order" }; break;
                default: throw new Exception("Unexpected endpoint");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(JsonSerializer.Serialize(new { return_code = 0, data })) });
        }
    }

    [Fact]
    public async Task DisposalClosesListeningPort()
    {
        var bridge = new DcTravelerBridge((_, _) => Task.FromResult<object?>(""));
        var port = bridge.Port;
        await bridge.DisposeAsync();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        await Assert.ThrowsAsync<HttpRequestException>(() => Post(http, port, "{}"));
    }
}
