namespace MinCms.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;

    /// <summary>
    /// Collection management service implementation.
    /// </summary>
    public class CollectionService : ICollectionService
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static string _Header = "[CollectionService] ";
        private IS3Service _S3Service = null;
        private LoggingModule _Logging = null;
        private SemaphoreSlim _CollectionsSemaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="s3Service">S3 service.</param>
        /// <param name="logging">Logging module.</param>
        public CollectionService(IS3Service s3Service, LoggingModule logging)
        {
            _S3Service = s3Service ?? throw new ArgumentNullException(nameof(s3Service));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<List<Collection>> GetAllCollectionsAsync(CancellationToken token = default)
        {
            _Logging.Debug(_Header + "retrieving all collections");
            List<Collection> collections = await _S3Service.LoadCollectionsAsync(token).ConfigureAwait(false);
            _Logging.Info(_Header + "retrieved " + collections.Count + " collections");
            return collections;
        }

        /// <inheritdoc />
        public async Task<Collection> GetCollectionBySlugAsync(string slug, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

            _Logging.Debug(_Header + "retrieving collection by slug '" + slug + "'");
            List<Collection> collections = await _S3Service.LoadCollectionsAsync(token).ConfigureAwait(false);
            Collection collection = collections.FirstOrDefault(b => String.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase));

            if (collection == null)
            {
                _Logging.Warn(_Header + "collection not found for slug '" + slug + "'");
                throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");
            }

            return collection;
        }

        /// <inheritdoc />
        public async Task<Collection> CreateCollectionAsync(string name, string slug, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

            if (String.Equals(slug, "config", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The slug 'config' is reserved and cannot be used.");

            _Logging.Info(_Header + "creating collection '" + name + "' with slug '" + slug + "'");

            await _CollectionsSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                string etag = await _S3Service.GetCollectionsETagAsync(token).ConfigureAwait(false);
                List<Collection> collections = await _S3Service.LoadCollectionsAsync(token).ConfigureAwait(false);

                bool slugExists = collections.Any(b => String.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase));
                if (slugExists)
                {
                    _Logging.Warn(_Header + "collection with slug '" + slug + "' already exists");
                    throw new InvalidOperationException("A collection with slug '" + slug + "' already exists.");
                }

                Collection collection = new Collection(name, slug);
                collections.Add(collection);

                await _S3Service.SaveCollectionsAsync(collections, etag, token).ConfigureAwait(false);

                _Logging.Info(_Header + "created collection '" + name + "' with slug '" + slug + "' and ID '" + collection.Id + "'");
                return collection;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _Logging.Warn(_Header + "exception creating collection '" + name + "':" + Environment.NewLine + ex.ToString());
                throw;
            }
            finally
            {
                _CollectionsSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task DeleteCollectionAsync(string slug, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

            _Logging.Info(_Header + "deleting collection with slug '" + slug + "'");

            await _CollectionsSemaphore.WaitAsync(token).ConfigureAwait(false);

            try
            {
                string etag = await _S3Service.GetCollectionsETagAsync(token).ConfigureAwait(false);
                List<Collection> collections = await _S3Service.LoadCollectionsAsync(token).ConfigureAwait(false);

                Collection collection = collections.FirstOrDefault(b => String.Equals(b.Slug, slug, StringComparison.OrdinalIgnoreCase));
                if (collection == null)
                {
                    _Logging.Warn(_Header + "collection not found for slug '" + slug + "'");
                    throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");
                }

                await _S3Service.DeletePrefixAsync(slug, token).ConfigureAwait(false);

                collections.Remove(collection);
                await _S3Service.SaveCollectionsAsync(collections, etag, token).ConfigureAwait(false);

                _Logging.Info(_Header + "deleted collection '" + collection.Name + "' with slug '" + slug + "'");
            }
            catch (Exception ex) when (ex is not KeyNotFoundException and not InvalidOperationException)
            {
                _Logging.Warn(_Header + "exception deleting collection slug '" + slug + "':" + Environment.NewLine + ex.ToString());
                throw;
            }
            finally
            {
                _CollectionsSemaphore.Release();
            }
        }

        /// <inheritdoc />
        public async Task<List<CollectionFile>> GetCollectionFilesAsync(string slug, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

            _Logging.Debug(_Header + "listing files for collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);
            List<CollectionFile> files = await _S3Service.ListFilesAsync(slug, token).ConfigureAwait(false);

            _Logging.Info(_Header + "found " + files.Count + " files for collection slug '" + slug + "'");
            return files;
        }

        /// <inheritdoc />
        public async Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
            if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));
            if (content == null) throw new ArgumentNullException(nameof(content));

            _Logging.Info(_Header + "uploading file '" + fileName + "' to collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);

            await _S3Service.UploadFileAsync(slug, fileName, content, contentType, token).ConfigureAwait(false);

            _Logging.Info(_Header + "uploaded file '" + fileName + "' to collection slug '" + slug + "'");
        }

        /// <inheritdoc />
        public async Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
            if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            _Logging.Debug(_Header + "downloading file '" + fileName + "' from collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);
            return await _S3Service.DownloadFileAsync(slug, fileName, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
            if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            _Logging.Info(_Header + "deleting file '" + fileName + "' from collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);

            bool exists = await _S3Service.FileExistsAsync(slug, fileName, token).ConfigureAwait(false);
            if (!exists)
            {
                _Logging.Warn(_Header + "file '" + fileName + "' not found in collection slug '" + slug + "'");
                throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");
            }

            await _S3Service.DeleteFileAsync(slug, fileName, token).ConfigureAwait(false);

            _Logging.Info(_Header + "deleted file '" + fileName + "' from collection slug '" + slug + "'");
        }

        /// <inheritdoc />
        public async Task<int> DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
            if (fileNames == null || fileNames.Count == 0) throw new ArgumentException("At least one filename is required.", nameof(fileNames));

            _Logging.Info(_Header + "batch deleting " + fileNames.Count + " files from collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);
            await _S3Service.DeleteFilesAsync(slug, fileNames, token).ConfigureAwait(false);

            _Logging.Info(_Header + "batch deleted " + fileNames.Count + " files from collection slug '" + slug + "'");
            return fileNames.Count;
        }

        /// <inheritdoc />
        public async Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
            if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

            _Logging.Debug(_Header + "getting metadata for file '" + fileName + "' in collection slug '" + slug + "'");

            Collection collection = await GetCollectionBySlugAsync(slug, token).ConfigureAwait(false);
            return await _S3Service.GetFileMetadataAsync(slug, fileName, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
