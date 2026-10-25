namespace Overmind.Mcp;

// Best-effort liveness: after any outbound response, POST a session touch at most once per
// window. The touch reuses the inner chain (never re-enters this handler), so it cannot
// self-trigger; a failed touch is logged and dropped — it must never fail the pipeline call.
public sealed class SessionTouchHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TouchTimeout = TimeSpan.FromSeconds(5);

    private const string TouchPathSuffix = "/touch";

    private readonly TimeSpan _window = Window;
    private long _lastTouchTicks;

    internal SessionTouchHandler(HttpMessageHandler inner, TimeSpan window)
        : this(inner)
    {
        _window = window;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        TryTouch(request);
        return response;
    }

    private void TryTouch(HttpRequestMessage request)
    {
        if (AdapterSession.Id is not { } id || id == Guid.Empty)
        {
            return;
        }

        var baseUri = request.RequestUri;
        if (baseUri is null || baseUri.AbsolutePath.EndsWith(TouchPathSuffix, StringComparison.Ordinal))
        {
            return;
        }

        var now = DateTime.UtcNow.Ticks;
        var last = Interlocked.Read(ref _lastTouchTicks);
        if (now - last < _window.Ticks)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _lastTouchTicks, now, last) != last)
        {
            return;
        }

        _ = TouchAsync(id, baseUri);
    }

    private async Task TouchAsync(Guid sessionId, Uri baseUri)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TouchTimeout);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                new Uri(baseUri, $"/api/agent/sessions/{sessionId}/touch"));
            using var response = await base.SendAsync(request, timeout.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[overmind-mcp] WARN: session touch failed: {ex.Message}");
        }
    }
}
