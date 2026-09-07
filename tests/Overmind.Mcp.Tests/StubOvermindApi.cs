using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Overmind.Mcp.Tests;

public sealed class StubOvermindApi : IAsyncDisposable
{
    public sealed record Request(string Method, string Path, string Query, string Body);

    public sealed record Response(int Status, string JsonBody);

    private sealed record StubRoute(string Method, System.Text.RegularExpressions.Regex Path, Func<Request, Response> Respond);

    private readonly HttpListener _listener;
    private readonly List<StubRoute> _routes = [];
    private readonly Task _loop;
    private readonly CancellationTokenSource _stop = new();

    public ConcurrentQueue<Request> Requests { get; } = new();

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    private StubOvermindApi(HttpListener listener, int port)
    {
        _listener = listener;
        Port = port;
        _loop = Task.Run(AcceptLoopAsync);
    }

    public static StubOvermindApi Start()
    {
        var port = TakeFreePort();
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        return new StubOvermindApi(listener, port);
    }

    public void RouteGet(string pathPattern, string jsonBody, int status = 200) =>
        Route("GET", pathPattern, status, jsonBody);

    public void Route(string method, string pathPattern, int status, string jsonBody) =>
        Route(method, pathPattern, _ => new Response(status, jsonBody));

    public void Route(string method, string pathPattern, Func<Request, Response> respond)
    {
        _routes.Insert(0, new StubRoute(method, new System.Text.RegularExpressions.Regex($"^{pathPattern}$"), respond));
    }

    public void Stop() => _listener.Close();

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try
        {
            _listener.Close();
        }
        catch (Exception)
        {
        }

        try
        {
            await _loop.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
        }
    }

    private static int TakeFreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = await reader.ReadToEndAsync();
            }

            var request = new Request(
                context.Request.HttpMethod,
                context.Request.Url?.AbsolutePath ?? string.Empty,
                context.Request.Url?.Query.TrimStart('?') ?? string.Empty,
                body);

            Requests.Enqueue(request);

            var route = _routes.FirstOrDefault(r =>
                r.Method == request.Method && r.Path.IsMatch(request.Path));

            var response = route?.Respond(request) ?? new Response(404, """{"title":"Not Found","detail":"stub has no route"}""");

            var bytes = Encoding.UTF8.GetBytes(response.JsonBody);
            context.Response.StatusCode = response.Status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception)
        {
            try
            {
                context.Response.Abort();
            }
            catch (Exception)
            {
            }
        }
    }
}
