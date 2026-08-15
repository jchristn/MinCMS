namespace MinCms.Test.Shared.Support
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Forward-only, non-seekable stream over an in-memory buffer.
    /// Used to exercise streaming code paths that cannot rely on Length or Seek.
    /// </summary>
    public sealed class NonSeekableReadStream : Stream
    {
        private readonly byte[] _Data;
        private int _Position;

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => _Position;
            set => throw new NotSupportedException();
        }

        /// <summary>Instantiate over a byte buffer.</summary>
        /// <param name="data">Backing buffer.</param>
        public NonSeekableReadStream(byte[] data)
        {
            _Data = data ?? throw new ArgumentNullException(nameof(data));
            _Position = 0;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (_Position >= _Data.Length) return 0;

            int read = Math.Min(count, _Data.Length - _Position);
            Buffer.BlockCopy(_Data, _Position, buffer, offset, read);
            _Position += read;
            return read;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_Position >= _Data.Length) return 0;

            int read = Math.Min(buffer.Length, _Data.Length - _Position);
            _Data.AsMemory(_Position, read).CopyTo(buffer);
            _Position += read;

            await Task.CompletedTask.ConfigureAwait(false);
            return read;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
