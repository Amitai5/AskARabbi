using AskARabbiLIB.DvarTorah.Audio;

namespace AskARabbi.Api.Tests;

internal sealed class FakeDvarTorahAudioReader : IDvarTorahAudioReader
{
    internal byte[] Bytes { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    internal DvarTorahAudioBlobInfo? Info { get; set; } = new(10, "\"test-etag\"", new DateTimeOffset(2026, 8, 24, 18, 0, 0, TimeSpan.Zero));

    internal DvarTorahAudioTimings? Timings { get; set; }

    internal Exception? Failure { get; set; }

    internal int InfoCalls { get; private set; }

    internal int TimingCalls { get; private set; }

    internal List<(long Offset, long? Length)> ReadCalls { get; } = [];

    internal bool WasStreamDisposed { get; private set; }

    public Task<DvarTorahAudioBlobInfo?> GetInfoAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InfoCalls++;
        return Failure is null ? Task.FromResult(Info) : Task.FromException<DvarTorahAudioBlobInfo?>(Failure);
    }

    public Task<Stream> OpenReadAsync(WeeklyDvarTorahAudioMetadata audio, long offset, long? length, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCalls.Add((offset, length));
        Stream stream = new NonSeekableReadStream(Bytes.AsSpan((int)offset, (int)(length ?? Bytes.Length - offset)).ToArray(), () => WasStreamDisposed = true);
        return Task.FromResult(stream);
    }

    public Task<DvarTorahAudioTimings?> GetTimingsAsync(WeeklyDvarTorahAudioMetadata audio, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TimingCalls++;
        return Failure is null ? Task.FromResult(Timings) : Task.FromException<DvarTorahAudioTimings?>(Failure);
    }

    private sealed class NonSeekableReadStream : Stream
    {
        private readonly MemoryStream content;
        private readonly Action onDispose;

        internal NonSeekableReadStream(byte[] bytes, Action onDispose)
        {
            content = new MemoryStream(bytes, writable: false);
            this.onDispose = onDispose;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => content.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => content.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                content.Dispose();
                onDispose();
            }
            base.Dispose(disposing);
        }
    }
}
