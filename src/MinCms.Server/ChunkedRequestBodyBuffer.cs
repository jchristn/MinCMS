namespace MinCms.Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;

    /// <summary>
    /// Buffers Watson chunked request bodies into a seekable stream.
    /// </summary>
    internal static class ChunkedRequestBodyBuffer
    {
        private const int _BufferSize = 65536;

        /// <summary>
        /// Create a seekable stream containing the dechunked request body.
        /// </summary>
        /// <param name="request">Watson request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Seekable body stream.</returns>
        public static async Task<Stream> CreateSeekableBodyStreamAsync(HttpRequestBase request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            if (!request.ChunkedTransfer)
            {
                return request.Data;
            }

            string tempPath = Path.Combine(Path.GetTempPath(), "mincms-request-body-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool success = false;

            try
            {
                using (FileStream writeStream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    _BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    while (true)
                    {
                        var chunk = await request.ReadChunk(token).ConfigureAwait(false);
                        if (chunk == null)
                        {
                            break;
                        }

                        if (chunk.Data != null && chunk.Data.Length > 0)
                        {
                            await writeStream.WriteAsync(chunk.Data.AsMemory(0, chunk.Data.Length), token).ConfigureAwait(false);
                        }

                        if (chunk.IsFinal)
                        {
                            break;
                        }
                    }

                    await writeStream.FlushAsync(token).ConfigureAwait(false);
                }

                FileStream readStream = new FileStream(
                    tempPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    _BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

                success = true;
                return readStream;
            }
            finally
            {
                if (!success && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }
    }
}
