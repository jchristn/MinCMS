namespace MinCms.Core.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for S3 storage operations.
    /// </summary>
    public interface IS3Service
    {
        /// <summary>
        /// Load collections configuration from S3.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of collections.</returns>
        Task<List<Collection>> LoadCollectionsAsync(CancellationToken token = default);

        /// <summary>
        /// Save collections configuration to S3 with optimistic concurrency.
        /// </summary>
        /// <param name="collections">Collections to save.</param>
        /// <param name="expectedETag">Expected ETag for concurrency check.</param>
        /// <param name="token">Cancellation token.</param>
        Task SaveCollectionsAsync(List<Collection> collections, string expectedETag, CancellationToken token = default);

        /// <summary>
        /// Get the current ETag for the collections configuration file.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ETag string.</returns>
        Task<string> GetCollectionsETagAsync(CancellationToken token = default);

        /// <summary>
        /// List all files under a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of collection files.</returns>
        Task<List<CollectionFile>> ListFilesAsync(string slug, CancellationToken token = default);

        /// <summary>
        /// Upload a file to a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="content">File content stream.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="token">Cancellation token.</param>
        Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default);

        /// <summary>
        /// Download a file from a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Download file result.</returns>
        Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Delete a file from a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Delete multiple files from a collection by filename.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileNames">List of filenames to delete.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default);

        /// <summary>
        /// Delete all objects under a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeletePrefixAsync(string slug, CancellationToken token = default);

        /// <summary>
        /// Get metadata for a specific file.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection file metadata.</returns>
        Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Check if a file exists under a collection prefix.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if file exists.</returns>
        Task<bool> FileExistsAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Ensure the collections configuration file exists in S3, creating an empty one if not.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        Task EnsureCollectionsConfigExistsAsync(CancellationToken token = default);
    }
}
