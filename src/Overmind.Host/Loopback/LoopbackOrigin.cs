using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Loopback;

public sealed class LoopbackOrigin(
    IServer server,
    IConfiguration configuration,
    HttpMessageHandler? innerHandler = null)
{
    public const int ClientTimeoutSeconds = 120;

    public HttpClient CreateClient(EmbeddedAgentSessionState state)
    {
        var inner = innerHandler ?? new SocketsHttpHandler { UseProxy = false };
        return new HttpClient(new EmbeddedAgentSessionHandler(inner, state))
        {
            BaseAddress = new Uri(ResolveAddress()),
            Timeout = TimeSpan.FromSeconds(ClientTimeoutSeconds)
        };
    }

    public string ResolveAddress()
    {
        var bound = server.Features.Get<IServerAddressesFeature>()?.Addresses
            .Select(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
                               uri.IsLoopback
                ? uri.GetLeftPart(UriPartial.Authority)
                : null)
            .FirstOrDefault(address => address is not null);
        if (bound is not null)
        {
            return bound;
        }

        var configured = configuration["Overmind:BindUrl"];
        if (!string.IsNullOrWhiteSpace(configured) &&
            Uri.TryCreate(configured, UriKind.Absolute, out var bindUri) &&
            bindUri.IsLoopback)
        {
            return bindUri.GetLeftPart(UriPartial.Authority);
        }

        return $"http://127.0.0.1:{OvermindPorts.DefaultPort}";
    }
}
