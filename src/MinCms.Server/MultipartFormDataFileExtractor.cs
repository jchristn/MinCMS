namespace MinCms.Server
{
    using MinCms.Core;
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Streaming multipart/form-data file extractor for single-file uploads.
    /// </summary>
    internal static class MultipartFormDataFileExtractor
    {
        private const int _BufferSize = 65536;
        private const int _MaxLineBytes = 16384;

        /// <summary>
        /// Extract a single uploaded file from a multipart/form-data request body.
        /// </summary>
        /// <param name="bodyStream">Request body stream.</param>
        /// <param name="boundary">Multipart boundary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Parsed multipart file.</returns>
        public static async Task<ParsedMultipartFile> ExtractSingleFileAsync(Stream bodyStream, string boundary, CancellationToken token = default)
        {
            if (bodyStream == null) throw new ArgumentNullException(nameof(bodyStream));
            if (String.IsNullOrWhiteSpace(boundary)) throw new ArgumentNullException(nameof(boundary));

            string boundaryLine = "--" + boundary;
            string line = await ReadLineAsync(bodyStream, token).ConfigureAwait(false);

            while (line != null && line.Length < 1)
            {
                line = await ReadLineAsync(bodyStream, token).ConfigureAwait(false);
            }

            if (!String.Equals(line, boundaryLine, StringComparison.Ordinal))
            {
                return null;
            }

            Dictionary<string, string> headers = await ReadHeadersAsync(bodyStream, token).ConfigureAwait(false);
            string contentDisposition = headers.TryGetValue("content-disposition", out string dispositionValue) ? dispositionValue : null;
            string fileName = ExtractHeaderParameter(contentDisposition, "filename*");

            if (!String.IsNullOrEmpty(fileName))
            {
                int encodingMarker = fileName.IndexOf("''", StringComparison.Ordinal);
                if (encodingMarker >= 0 && encodingMarker + 2 < fileName.Length)
                {
                    fileName = fileName.Substring(encodingMarker + 2);
                }
            }

            if (String.IsNullOrEmpty(fileName))
            {
                fileName = ExtractHeaderParameter(contentDisposition, "filename");
            }

            if (String.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string contentType =
                headers.TryGetValue("content-type", out string contentTypeValue) && !String.IsNullOrWhiteSpace(contentTypeValue)
                ? contentTypeValue
                : Constants.BinaryContentType;

            string tempPath = Path.Combine(Path.GetTempPath(), "mincms-upload-" + Guid.NewGuid().ToString("N") + ".tmp");
            bool success = false;

            try
            {
                long contentLength;

                using (FileStream writeStream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    _BufferSize,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    contentLength = await CopyBodyToTempFileAsync(bodyStream, writeStream, boundary, token).ConfigureAwait(false);
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

                return new ParsedMultipartFile
                {
                    FileName = fileName,
                    ContentType = contentType,
                    FileStream = readStream,
                    ContentLength = contentLength
                };
            }
            finally
            {
                if (!success && File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static async Task<Dictionary<string, string>> ReadHeadersAsync(Stream bodyStream, CancellationToken token)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            while (true)
            {
                string line = await ReadLineAsync(bodyStream, token).ConfigureAwait(false);
                if (line == null) throw new FormatException("Multipart headers terminated unexpectedly.");
                if (line.Length < 1) break;

                int separator = line.IndexOf(':');
                if (separator <= 0) continue;

                string name = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                if (name.Length > 0)
                {
                    headers[name] = value;
                }
            }

            return headers;
        }

        private static async Task<string> ReadLineAsync(Stream stream, CancellationToken token)
        {
            byte[] lineBuffer = ArrayPool<byte>.Shared.Rent(_MaxLineBytes);

            try
            {
                int count = 0;

                while (true)
                {
                    int nextByte = await ReadByteAsync(stream, token).ConfigureAwait(false);
                    if (nextByte < 0)
                    {
                        if (count < 1) return null;
                        break;
                    }

                    if (nextByte == '\n')
                    {
                        break;
                    }

                    if (count >= _MaxLineBytes)
                    {
                        throw new FormatException("Multipart header line exceeds the supported length.");
                    }

                    lineBuffer[count++] = (byte)nextByte;
                }

                if (count > 0 && lineBuffer[count - 1] == '\r')
                {
                    count--;
                }

                return Encoding.UTF8.GetString(lineBuffer, 0, count);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(lineBuffer);
            }
        }

        private static async Task<int> ReadByteAsync(Stream stream, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1);

            try
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, 1), token).ConfigureAwait(false);
                return read == 1 ? buffer[0] : -1;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static async Task<long> CopyBodyToTempFileAsync(Stream source, Stream destination, string boundary, CancellationToken token)
        {
            byte[] boundaryBytes = Encoding.ASCII.GetBytes("\r\n--" + boundary);
            int lookBehindCount = boundaryBytes.Length + 4;
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_BufferSize + lookBehindCount);
            long written = 0;
            int buffered = 0;

            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(buffered, _BufferSize), token).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    buffered += read;
                    int searchOffset = 0;

                    while (true)
                    {
                        int boundaryIndex = IndexOf(buffer, buffered, boundaryBytes, searchOffset);
                        if (boundaryIndex < 0)
                        {
                            break;
                        }

                        int suffixOffset = boundaryIndex + boundaryBytes.Length;
                        if (buffered < suffixOffset + 2)
                        {
                            break;
                        }

                        bool isFinalBoundary = buffer[suffixOffset] == (byte)'-' && buffer[suffixOffset + 1] == (byte)'-';
                        bool isPartBoundary = buffer[suffixOffset] == (byte)'\r' && buffer[suffixOffset + 1] == (byte)'\n';

                        if (!isFinalBoundary && !isPartBoundary)
                        {
                            searchOffset = boundaryIndex + 1;
                            continue;
                        }

                        if (boundaryIndex > 0)
                        {
                            await destination.WriteAsync(buffer.AsMemory(0, boundaryIndex), token).ConfigureAwait(false);
                            written += boundaryIndex;
                        }

                        await DrainToEndAsync(source, token).ConfigureAwait(false);
                        return written;
                    }

                    int bytesToKeep = Math.Min(lookBehindCount, buffered);
                    int bytesToWrite = buffered - bytesToKeep;

                    if (bytesToWrite > 0)
                    {
                        await destination.WriteAsync(buffer.AsMemory(0, bytesToWrite), token).ConfigureAwait(false);
                        written += bytesToWrite;
                        Buffer.BlockCopy(buffer, bytesToWrite, buffer, 0, bytesToKeep);
                        buffered = bytesToKeep;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            throw new FormatException("Multipart form data terminated before the final boundary.");
        }

        private static async Task DrainToEndAsync(Stream stream, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(_BufferSize);

            try
            {
                while (await stream.ReadAsync(buffer.AsMemory(0, _BufferSize), token).ConfigureAwait(false) > 0)
                {
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static int IndexOf(byte[] haystack, int haystackLength, byte[] needle, int startIndex)
        {
            if (needle == null || haystack == null) return -1;
            if (needle.Length < 1 || haystackLength < needle.Length || startIndex < 0) return -1;

            for (int i = startIndex; i <= haystackLength - needle.Length; i++)
            {
                bool match = true;

                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string ExtractHeaderParameter(string headerValue, string parameterName)
        {
            if (String.IsNullOrEmpty(headerValue) || String.IsNullOrEmpty(parameterName))
            {
                return null;
            }

            string marker = parameterName + "=";
            int start = headerValue.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            start += marker.Length;
            if (start >= headerValue.Length)
            {
                return null;
            }

            if (headerValue[start] == '"')
            {
                start++;
                int end = headerValue.IndexOf('"', start);
                return end > start ? headerValue.Substring(start, end - start) : null;
            }

            int semicolon = headerValue.IndexOf(';', start);
            int endIndex = semicolon >= 0 ? semicolon : headerValue.Length;
            return endIndex > start ? headerValue.Substring(start, endIndex - start).Trim() : null;
        }
    }
}
