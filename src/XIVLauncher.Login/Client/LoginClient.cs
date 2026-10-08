using System.Collections.Frozen;
using XIVLauncher.Login.Channels;
using XIVLauncher.Login.Models;

namespace XIVLauncher.Login.Client;

public sealed class LoginClient
{
    public async Task<LoginResult> LoginAsync(LoginType loginType, LoginRequest request, CancellationToken cancellationToken = default)
    {
        var channels = DiscoverChannels(new LoginChannelContext(request.DeviceProfile));

        if (!channels.TryGetValue(loginType, out var channel))
            throw new ArgumentOutOfRangeException(nameof(loginType), loginType, $"未知登录渠道: {loginType}");

        cancellationToken.ThrowIfCancellationRequested();
        return await channel.LoginAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static FrozenDictionary<LoginType, ILoginChannel> DiscoverChannels(LoginChannelContext context)
    {
        var loginChannels = typeof(ILoginChannel)
                            .Assembly
                            .GetTypes()
                            .Where(type => type is { IsAbstract: false, IsInterface: false })
                            .Where(type => typeof(ILoginChannel).IsAssignableFrom(type))
                            .Select(type => (ILoginChannel?)Activator.CreateInstance(type, context))
                            .Where(channel => channel != null)
                            .Cast<ILoginChannel>()
                            .ToArray();

        var duplicatedType = loginChannels
                             .GroupBy(channel => channel.Type)
                             .FirstOrDefault(group => group.Count() > 1);

        if (duplicatedType != null)
            throw new InvalidOperationException($"发现重复 LoginType 渠道实现: {duplicatedType.Key}");

        return loginChannels.ToFrozenDictionary(channel => channel.Type);
    }
}
