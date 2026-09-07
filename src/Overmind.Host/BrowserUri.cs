namespace Overmind.Host;

public static class BrowserUri
{
    public static Uri FromLoopback(Uri bound) =>
        bound.Host is "127.0.0.1" or "localhost" or "[::1]"
            ? new UriBuilder(bound) { Host = "overmind.localhost" }.Uri
            : bound;
}
