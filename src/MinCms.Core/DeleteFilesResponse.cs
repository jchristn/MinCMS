namespace MinCms.Core
{
    /// <summary>
    /// Response body for batch file deletion.
    /// </summary>
    public class DeleteFilesResponse
    {
        /// <summary>
        /// Number of files deleted.
        /// </summary>
        public int DeletedCount { get; set; }
    }
}
