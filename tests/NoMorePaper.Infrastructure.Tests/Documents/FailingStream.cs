namespace NoMorePaper.Infrastructure.Tests.Documents;

/// <summary>Delivers data in small chunks and breaks off after a given number of bytes, like a dropped upload.</summary>
internal sealed class FailingStream(byte[] data, int failAfterBytes) : Stream
{
    private const int ChunkSize = 4096;
    private int _position;

    /// <summary>Runs before each read, e.g. to cancel the copy halfway.</summary>
    public Action? OnRead { get; init; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        if (_position > 0)
        {
            OnRead?.Invoke();
        }

        if (_position >= failAfterBytes)
        {
            throw new IOException("Connection dropped.");
        }

        var count = Math.Min(Math.Min(buffer.Length, ChunkSize), data.Length - _position);
        data.AsSpan(_position, count).CopyTo(buffer);
        _position += count;
        return count;
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Read(buffer.Span));
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
