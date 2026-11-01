using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Overmind.Mcp.Tests;

public sealed class StubOvermindApi : IAsyncDisposable
{
    private static readonly System.Text.RegularExpressions.Regex SessionTouchPath =
        new("^/api/agent/sessions/[0-9a-fA-F-]{36}/touch$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private const int ChunkTerminal = -1;
    private const int ChunkNeedMore = -2;

    public sealed record Request(string Method, string Path, string Query, string Body, IReadOnlyDictionary<string, string> Headers);

    public sealed record Response(int Status, string JsonBody);

    private sealed record StubRoute(string Method, System.Text.RegularExpressions.Regex Path, Func<Request, Response> Respond);

    private readonly TcpListener _listener;
    private readonly List<StubRoute> _routes = [];
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private bool _closed;

    public ConcurrentQueue<Request> Requests { get; } = new();

    public ConcurrentQueue<Request> TouchRequests { get; } = new();

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}";

    private StubOvermindApi(TcpListener listener, int port)
    {
        _listener = listener;
        Port = port;
        _loop = Task.Run(AcceptLoopAsync);
    }

    public static StubOvermindApi Start()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new StubOvermindApi(listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    public void RouteGet(string pathPattern, string jsonBody, int status = 200) =>
        Route("GET", pathPattern, status, jsonBody);

    public void Route(string method, string pathPattern, int status, string jsonBody) =>
        Route(method, pathPattern, _ => new Response(status, jsonBody));

    public void Route(string method, string pathPattern, Func<Request, Response> respond)
    {
        lock (_routes)
        {
            _routes.Insert(0, new StubRoute(method, new System.Text.RegularExpressions.Regex($"^{pathPattern}$"), respond));
        }
    }

    public void Stop()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        _listener.Stop();
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        Stop();
        try
        {
            await _loop.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            using (client)
            await using (var stream = client.GetStream())
            {
                var request = await ReadRequestAsync(stream);
                if (request is not null)
                {
                    await WriteResponseAsync(stream, Respond(request));
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private static async Task<Request?> ReadRequestAsync(NetworkStream stream)
    {
        var received = new MemoryStream();
        var buffer = new byte[8192];
        var headerEnd = -1;
        while (headerEnd < 0)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                return null;
            }

            received.Write(buffer, 0, read);
            headerEnd = IndexOfHeaderEnd(received);
            if (headerEnd < 0 && received.Length > 64 * 1024)
            {
                return null;
            }
        }

        var head = Encoding.ASCII.GetString(received.GetBuffer(), 0, headerEnd);
        var lines = head.Split("\r\n");
        var target = lines[0].Split(' ');
        if (target.Length < 2)
        {
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator > 0)
            {
                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        var chunked = headers.TryGetValue("Transfer-Encoding", out var transferEncoding) &&
                      transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase);
        var contentLength = headers.TryGetValue("Content-Length", out var rawLength) &&
                            int.TryParse(rawLength, out var parsedLength)
            ? parsedLength
            : 0;
        var body = chunked
            ? Encoding.UTF8.GetString(await ReadChunkedBodyAsync(stream, received, headerEnd + 4))
            : Encoding.UTF8.GetString(await ReadSizedBodyAsync(stream, received, headerEnd + 4, contentLength));

        var pathAndQuery = target[1];
        var queryStart = pathAndQuery.IndexOf('?');
        var path = queryStart >= 0 ? pathAndQuery[..queryStart] : pathAndQuery;
        var query = queryStart >= 0 ? pathAndQuery[(queryStart + 1)..] : string.Empty;

        return new Request(target[0], path, query, body, headers);
    }

    private static async Task<byte[]> ReadSizedBodyAsync(NetworkStream stream, MemoryStream received, int bodyStart, int contentLength)
    {
        var buffer = new byte[8192];
        while (received.Length < bodyStart + (long)contentLength)
        {
            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                break;
            }

            received.Write(buffer, 0, read);
        }

        var available = Math.Min(contentLength, (int)received.Length - bodyStart);
        var body = new byte[available];
        Buffer.BlockCopy(received.GetBuffer(), bodyStart, body, 0, available);
        return body;
    }

    private static async Task<byte[]> ReadChunkedBodyAsync(NetworkStream stream, MemoryStream received, int bodyStart)
    {
        var buffer = new byte[8192];
        using var body = new MemoryStream();
        var position = bodyStart;
        while (true)
        {
            var next = TryConsumeChunk(received, position, body);
            if (next >= 0)
            {
                position = next;
                continue;
            }

            if (next == ChunkTerminal)
            {
                return body.ToArray();
            }

            var read = await stream.ReadAsync(buffer);
            if (read == 0)
            {
                return body.ToArray();
            }

            received.Write(buffer, 0, read);
        }
    }

    private static int TryConsumeChunk(MemoryStream received, int position, MemoryStream body)
    {
        var bytes = received.GetBuffer();
        var end = (int)received.Length;
        var lineEnd = IndexOfByte(bytes, position, end, (byte)'\n');
        if (lineEnd < 0)
        {
            return ChunkNeedMore;
        }

        var sizeToken = Encoding.ASCII.GetString(bytes, position, lineEnd - position).Trim();
        var extension = sizeToken.IndexOf(';');
        if (extension >= 0)
        {
            sizeToken = sizeToken[..extension];
        }

        var size = Convert.ToInt32(sizeToken, 16);
        var chunkStart = lineEnd + 1;
        if (chunkStart + size + 2 > end)
        {
            return ChunkNeedMore;
        }

        if (size > 0)
        {
            body.Write(bytes, chunkStart, size);
            return chunkStart + size + 2;
        }

        var cursor = chunkStart;
        while (true)
        {
            var trailerEnd = IndexOfByte(bytes, cursor, end, (byte)'\n');
            if (trailerEnd < 0)
            {
                return ChunkNeedMore;
            }

            var blank = trailerEnd == cursor || (trailerEnd == cursor + 1 && bytes[cursor] == (byte)'\r');
            cursor = trailerEnd + 1;
            if (blank)
            {
                return ChunkTerminal;
            }
        }
    }

    private static int IndexOfByte(byte[] bytes, int start, int end, byte value)
    {
        for (var i = start; i < end; i++)
        {
            if (bytes[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfHeaderEnd(MemoryStream received)
    {
        var bytes = received.GetBuffer();
        var end = (int)received.Length;
        for (var i = 0; i + 3 < end; i++)
        {
            if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n')
            {
                return i;
            }
        }

        return -1;
    }

    private Response Respond(Request request)
    {
        if (request.Method == "POST" && SessionTouchPath.IsMatch(request.Path))
        {
            TouchRequests.Enqueue(request);
            return new Response(204, string.Empty);
        }

        Requests.Enqueue(request);

        lock (_routes)
        {
            var route = _routes.FirstOrDefault(r => r.Method == request.Method && r.Path.IsMatch(request.Path));
            return route?.Respond(request) ?? new Response(404, """{"title":"Not Found","detail":"stub has no route"}""");
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, Response response)
    {
        await stream.WriteAsync(BuildResponseHead(response));
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response.JsonBody));
    }

    private static byte[] BuildResponseHead(Response response)
    {
        var builder = new StringBuilder();
        builder
            .Append("HTTP/1.1 ")
            .Append(response.Status)
            .Append(' ')
            .Append(((HttpStatusCode)response.Status).ToString())
            .Append("\r\n");
        if (response.Status != 204)
        {
            builder
                .Append("Content-Type: application/json\r\n")
                .Append("Content-Length: ")
                .Append(Encoding.UTF8.GetByteCount(response.JsonBody))
                .Append("\r\n");
        }

        builder.Append("Connection: close\r\n\r\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
