namespace MinCms.Server.Tests
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class NonSeekableReadStream : Stream
    {
        private readonly byte[] _Data;
        private int _Position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _Position;
            set => throw new NotSupportedException();
        }

        public NonSeekableReadStream(byte[] data)
        {
            _Data = data ?? throw new ArgumentNullException(nameof(data));
            _Position = 0;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (_Position >= _Data.Length) return 0;

            int read = Math.Min(count, _Data.Length - _Position);
            Buffer.BlockCopy(_Data, _Position, buffer, offset, read);
            _Position += read;
            return read;
        }

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

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
