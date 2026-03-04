namespace MinCms.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Request body for batch file deletion.
    /// </summary>
    public class DeleteFilesRequest
    {
        /// <summary>
        /// List of filenames to delete.
        /// </summary>
        public List<string> FileNames { get; set; } = new List<string>();
    }
}
