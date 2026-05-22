namespace MinCms.Core.Services
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only bounded window over a seekable base stream.
    /// </summary>
    internal sealed class ReadOnlySubStream : Stream
    {
        private readonly Stream _BaseStream;
        private readonly bool _LeaveOpen;
        private readonly long _Length;
        private long _Position;

        public override bool CanRead => _BaseStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _Length;

        public override long Position
        {
            get => _Position;
            set => throw new NotSupportedException();
        }

        public ReadOnlySubStream(Stream baseStream, long offset, long length, bool leaveOpen = true)
        {
            if (baseStream == null) throw new ArgumentNullException(nameof(baseStream));
            if (!baseStream.CanSeek) throw new ArgumentException("Base stream must be seekable.", nameof(baseStream));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

            _BaseStream = baseStream;
            _LeaveOpen = leaveOpen;
            _Length = length;
            _Position = 0;

            _BaseStream.Seek(offset, SeekOrigin.Begin);
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Position >= _Length) return 0;

            int maxCount = (int)Math.Min(count, _Length - _Position);
            int read = _BaseStream.Read(buffer, offset, maxCount);
            _Position += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_Position >= _Length) return 0;

            int maxCount = (int)Math.Min(buffer.Length, _Length - _Position);
            int read = await _BaseStream.ReadAsync(buffer[..maxCount], cancellationToken).ConfigureAwait(false);
            _Position += read;
            return read;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (buffer.Length - offset < count) throw new ArgumentException("The buffer is too small for the requested read.");

            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_LeaveOpen)
            {
                _BaseStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
