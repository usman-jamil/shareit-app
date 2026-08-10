namespace Share.Infrastructure.Storage;

/// <summary>
/// A read-only pass-through that counts the bytes read out of it, so an upload in flight can
/// say how far it has got.
/// </summary>
/// <remarks>
/// Stays seekable and keeps reporting its length: <see cref="StreamContent"/> derives
/// <c>Content-Length</c> from those, and a presigned URL is signed for a request that
/// declares its length rather than one that arrives chunked. Wrapping the file in something
/// unseekable would turn every upload into a 411 or a signature mismatch.
/// </remarks>
internal sealed class ProgressReportingStream(Stream inner, IProgress<long> progress) : Stream
{
    private long _bytesRead;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => inner.CanSeek;

    public override bool CanWrite => false;

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set
        {
            inner.Position = value;
            Restate(value);
        }
    }

    public override void Flush() => inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        Counted(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Counted(inner.Read(buffer));

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        Counted(await inner.ReadAsync(buffer, cancellationToken));

    public override long Seek(long offset, SeekOrigin origin)
    {
        long position = inner.Seek(offset, origin);

        Restate(position);

        return position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Counted(int count)
    {
        _bytesRead += count;

        progress.Report(_bytesRead);

        return count;
    }

    /// <summary>
    /// Restates the count from a position rather than adding to it. The body is rewound if
    /// the request is retried, and a total that carried on climbing would be counting bytes
    /// that are being sent for the second time.
    /// </summary>
    private void Restate(long position)
    {
        _bytesRead = position;

        progress.Report(_bytesRead);
    }
}
