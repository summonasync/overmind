using System.Text;

namespace Overmind.Host.Api;

public sealed class ResponseCaptureStream(Stream inner) : Stream
{
    private readonly MemoryStream _capture = new();

    public string CapturedBody => Encoding.UTF8.GetString(_capture.GetBuffer(), 0, (int)_capture.Length);

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => _capture.Length;

    public override long Position
    {
        get => _capture.Position;
        set => throw new NotSupportedException();
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        _capture.Flush();
        return inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _capture.Write(buffer, offset, count);
        inner.Write(buffer, offset, count);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        _capture.Write(buffer.Span);
        return inner.WriteAsync(buffer, cancellationToken);
    }
}
