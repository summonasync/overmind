using System.Text;

namespace Overmind.Api.Tests;

public sealed class SseFrameReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[4096];
    private readonly StringBuilder _pending = new();

    public async Task<string?> ReadFrameTextAsync(CancellationToken ct = default)
    {
        while (true)
        {
            var frame = TakeFrame();
            if (frame is not null)
            {
                return frame;
            }

            var read = await stream.ReadAsync(_buffer, ct);
            if (read == 0)
            {
                return null;
            }

            _pending.Append(Encoding.UTF8.GetString(_buffer, 0, read));
        }
    }

    private string? TakeFrame()
    {
        var text = _pending.ToString();
        var boundary = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (boundary < 0)
        {
            return null;
        }

        var frame = text[..(boundary + 2)];
        _pending.Remove(0, boundary + 2);
        return frame;
    }
}
