namespace Overmind.Host;

public sealed class BrowserOrigin
{
    private volatile Uri? _origin;

    public void Bind(Uri origin) => _origin = origin;

    public Uri Resolve(HttpContext context) =>
        _origin ?? BrowserUri.FromLoopback(new Uri($"{context.Request.Scheme}://{context.Request.Host}"));
}
