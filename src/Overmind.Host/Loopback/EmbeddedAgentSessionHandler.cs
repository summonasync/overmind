using Overmind.Host.Api;
using Overmind.Infrastructure.Agent;

namespace Overmind.Host.Loopback;

public sealed class EmbeddedAgentSessionHandler(HttpMessageHandler inner, EmbeddedAgentSessionState state)
    : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (state.IsRegistered)
        {
            request.Headers.Add(SessionHeader.Name, state.SessionId.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
