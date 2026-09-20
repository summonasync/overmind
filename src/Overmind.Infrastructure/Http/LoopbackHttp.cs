using System.Net.Http;
using System.Net.Sockets;

namespace Overmind.Infrastructure.Http;

internal static class LoopbackHttp
{
    public static bool IsLoopback(Uri uri) => uri.IsLoopback;

    public static SocketsHttpHandler CreateProxyBypassHandler() => new() { UseProxy = false };

    public static HttpMessageHandler CreateHandler(Uri uri) =>
        IsLoopback(uri) ? CreateProxyBypassHandler() : new HttpClientHandler();

    public static HttpMessageHandler CreateHandler(string baseUrl) =>
        Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ? CreateHandler(uri) : new HttpClientHandler();
}
