namespace UblTr.Validator.Xml;

/// <summary>Read-only pass-through stream that counts bytes, so reports can state document size without buffering.</summary>
internal sealed class CountingStream(Stream inner) : Stream
{
    public long BytesRead { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesRead;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var n = inner.Read(buffer, offset, count);
        BytesRead += n;
        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        var n = inner.Read(buffer);
        BytesRead += n;
        return n;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
