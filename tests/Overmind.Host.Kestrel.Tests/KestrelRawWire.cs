using System.Net.Sockets;
using System.Text;

namespace Overmind.Host.Kestrel.Tests;

public static class KestrelRawWire
{
    public static async Task<RawKestrelResponse> SendAsync(Uri baseAddress, string requestLine, string? jsonBody = null)
    {
        using var socket = new TcpClient();
        await socket.ConnectAsync(baseAddress.Host, baseAddress.Port);
        await using var stream = socket.GetStream();
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var head = new StringBuilder();
        head.Append(requestLine).Append("\r\n");
        head.Append($"Host: {baseAddress.Host}:{baseAddress.Port}\r\n");
        head.Append("Connection: close\r\n");
        byte[]? payload = null;
        if (jsonBody is not null)
        {
            payload = Encoding.UTF8.GetBytes(jsonBody);
            head.Append($"Content-Type: application/json\r\nContent-Length: {payload.Length}\r\n");
        }

        head.Append("\r\n");

        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString()));
        if (payload is not null)
        {
            await stream.WriteAsync(payload);
        }

        await stream.FlushAsync();

        var wire = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, readTimeout.Token)) > 0)
        {
            await wire.WriteAsync(chunk, 0, read, readTimeout.Token);
        }

        return RawKestrelResponse.Parse(wire.ToArray());
    }
}
