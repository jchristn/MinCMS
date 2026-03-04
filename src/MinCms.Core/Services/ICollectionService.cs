namespace MinCms.Core.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interface for collection management operations.
    /// </summary>
    public interface ICollectionService
    {
        /// <summary>
        /// Get all collections.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of collections.</returns>
        Task<List<Collection>> GetAllCollectionsAsync(CancellationToken token = default);

        /// <summary>
        /// Get a collection by its slug.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection.</returns>
        Task<Collection> GetCollectionBySlugAsync(string slug, CancellationToken token = default);

        /// <summary>
        /// Create a new collection.
        /// </summary>
        /// <param name="name">Collection name.</param>
        /// <param name="slug">URL-friendly slug.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created collection.</returns>
        Task<Collection> CreateCollectionAsync(string name, string slug, CancellationToken token = default);

        /// <summary>
        /// Delete a collection and all its files.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeleteCollectionAsync(string slug, CancellationToken token = default);

        /// <summary>
        /// Get all files for a collection.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of collection files.</returns>
        Task<List<CollectionFile>> GetCollectionFilesAsync(string slug, CancellationToken token = default);

        /// <summary>
        /// Upload a file to a collection.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="content">File content stream.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="token">Cancellation token.</param>
        Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default);

        /// <summary>
        /// Download a file from a collection.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Download file result.</returns>
        Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Delete a file from a collection.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default);

        /// <summary>
        /// Delete multiple files from a collection.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileNames">List of filenames to delete.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Number of files deleted.</returns>
        Task<int> DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default);

        /// <summary>
        /// Get metadata for a specific file.
        /// </summary>
        /// <param name="slug">Collection slug.</param>
        /// <param name="fileName">File name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Collection file metadata.</returns>
        Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default);
    }
}
