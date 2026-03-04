namespace MinCms.Server
{
    using System.IO;

    /// <summary>
    /// Parsed result from multipart form data.
    /// </summary>
    public class ParsedMultipartFile
    {
        /// <summary>
        /// File name from the multipart form data.
        /// </summary>
        public string FileName { get; set; } = null;

        /// <summary>
        /// Content type from the multipart form data.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// File content stream.
        /// </summary>
        public Stream FileStream { get; set; } = null;
    }
}
