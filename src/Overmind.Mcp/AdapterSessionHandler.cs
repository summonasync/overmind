namespace Overmind.Mcp;

public sealed class AdapterSessionHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(AdapterVersion.HeaderName, AdapterVersion.Current);

        if (AdapterSession.Id is { } id)
        {
            request.Headers.Add(AdapterSession.HeaderName, id.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
