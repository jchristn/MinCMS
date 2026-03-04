namespace MinCms.Core
{
    using System.IO;

    /// <summary>
    /// Result of a file download operation.
    /// </summary>
    public class DownloadFileResult
    {
        /// <summary>
        /// File content stream.
        /// </summary>
        public Stream Content { get; set; } = null;

        /// <summary>
        /// Content type of the file.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Content length in bytes.
        /// </summary>
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// File name.
        /// </summary>
        public string FileName { get; set; } = null;
    }
}
