namespace MinCms.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon;
    using Amazon.S3;
    using Amazon.S3.Model;
    using MinCms.Core.Settings;
    using MinCms.Core.Telemetry;
    using SyslogLogging;

    /// <summary>
    /// S3 storage service implementation.
    /// </summary>
    public class S3Service : IS3Service
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static string _Header = "[S3Service] ";
        private S3Settings _S3Settings = null;
        private LoggingModule _Logging = null;
        private IS3ClientAdapter _S3Client = null;
        private Serialization.Serializer _Serializer = new Serialization.Serializer();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="s3Settings">S3 settings.</param>
        /// <param name="logging">Logging module.</param>
        public S3Service(S3Settings s3Settings, LoggingModule logging)
            : this(s3Settings, logging, null)
        {
        }

        /// <summary>
        /// Instantiate using a supplied S3 client adapter.
        /// </summary>
        /// <param name="s3Settings">S3 settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="s3Client">S3 client adapter.</param>
        public S3Service(S3Settings s3Settings, LoggingModule logging, IS3ClientAdapter s3Client)
        {
            _S3Settings = s3Settings ?? throw new ArgumentNullException(nameof(s3Settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _S3Client = s3Client ?? CreateClientAdapter(_S3Settings);
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<List<Collection>> LoadCollectionsAsync(CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("load_collections", async () =>
            {
                _Logging.Debug(_Header + "loading collections configuration from S3");

                try
                {
                    GetObjectRequest request = new GetObjectRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = Constants.CollectionsConfigKey
                    };

                    GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false);

                    using (StreamReader reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
                    {
                        string json = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                        StorageTelemetry.RecordBytesRead(Encoding.UTF8.GetByteCount(json));
                        List<Collection> collections = _Serializer.DeserializeJson<List<Collection>>(json);
                        _Logging.Debug(_Header + "loaded " + collections.Count + " collections from S3");
                        return collections;
                    }
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _Logging.Warn(_Header + "collections configuration not found in S3, returning empty list");
                    return new List<Collection>();
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception loading collections configuration:" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task SaveCollectionsAsync(List<Collection> collections, string expectedETag, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("save_collections", async () =>
            {
                if (collections == null) throw new ArgumentNullException(nameof(collections));

                _Logging.Debug(_Header + "saving collections configuration to S3 with ETag " + (expectedETag ?? "(none)"));

                try
                {
                    string json = _Serializer.SerializeJson(collections, true);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);

                    PutObjectRequest request = new PutObjectRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = Constants.CollectionsConfigKey,
                        ContentType = Constants.JsonContentType,
                        InputStream = new MemoryStream(bytes)
                    };
                    ApplyCompatibilityMetadata(request);

                    await _S3Client.PutObjectAsync(request, token).ConfigureAwait(false);
                    StorageTelemetry.RecordBytesWritten(bytes.Length);

                    _Logging.Info(_Header + "saved collections configuration to S3 (" + collections.Count + " collections)");
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.PreconditionFailed)
                {
                    _Logging.Warn(_Header + "ETag mismatch saving collections configuration:" + Environment.NewLine + ex.ToString());
                    throw new InvalidOperationException("Collections configuration was modified by another operation. Please retry.", ex);
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception saving collections configuration:" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task<string> GetCollectionsETagAsync(CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("get_collections_etag", async () =>
            {
                _Logging.Debug(_Header + "getting collections configuration ETag");

                try
                {
                    GetObjectMetadataRequest request = new GetObjectMetadataRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = Constants.CollectionsConfigKey
                    };

                    GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);
                    return response.ETag;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return null;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception getting collections ETag:" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task<List<CollectionFile>> ListFilesAsync(string slug, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("list_objects", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

                _Logging.Debug(_Header + "listing files for collection slug '" + slug + "'");

                List<CollectionFile> files = new List<CollectionFile>();
                string prefix = slug + "/";
                string continuationToken = null;

                do
                {
                    ListObjectsV2Request request = new ListObjectsV2Request
                    {
                        BucketName = _S3Settings.Bucket,
                        Prefix = prefix,
                        ContinuationToken = continuationToken
                    };

                    ListObjectsV2Response response = await _S3Client.ListObjectsV2Async(request, token).ConfigureAwait(false);

                    foreach (S3Object obj in response.S3Objects ?? new List<S3Object>())
                    {
                        string fileName = obj.Key.Substring(prefix.Length);
                        if (String.IsNullOrEmpty(fileName)) continue;

                        CollectionFile collectionFile = new CollectionFile
                        {
                            Key = obj.Key,
                            FileName = Uri.UnescapeDataString(fileName),
                            Size = obj.Size ?? 0,
                            LastModifiedUtc = obj.LastModified.HasValue ? obj.LastModified.Value.ToUniversalTime() : DateTime.UtcNow,
                            ETag = obj.ETag
                        };

                        files.Add(collectionFile);
                    }

                    continuationToken = (response.IsTruncated == true) ? response.NextContinuationToken : null;

                } while (continuationToken != null);

                _Logging.Info(_Header + "found " + files.Count + " files for collection slug '" + slug + "'");
                return files;
            });
        }

        /// <inheritdoc />
        public Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("put_object", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));
                if (content == null) throw new ArgumentNullException(nameof(content));

                string key = slug + "/" + Uri.EscapeDataString(fileName);
                _Logging.Debug(_Header + "uploading file '" + key + "' to S3");
                PreparedUploadSource preparedSource = null;

                try
                {
                    preparedSource = await PrepareUploadSourceAsync(content, token).ConfigureAwait(false);
                    string resolvedContentType = !String.IsNullOrEmpty(contentType) ? contentType : Constants.BinaryContentType;

                    if (ShouldUseMultipartUpload(preparedSource.Length))
                    {
                        await UploadFileMultipartAsync(key, preparedSource, resolvedContentType, token).ConfigureAwait(false);
                    }
                    else
                    {
                        if (preparedSource.Stream.CanSeek)
                        {
                            preparedSource.Stream.Seek(preparedSource.StartPosition, SeekOrigin.Begin);
                        }

                        PutObjectRequest request = new PutObjectRequest
                        {
                            BucketName = _S3Settings.Bucket,
                            Key = key,
                            ContentType = resolvedContentType,
                            InputStream = preparedSource.Stream
                        };
                        ApplyCompatibilityMetadata(request);

                        await _S3Client.PutObjectAsync(request, token).ConfigureAwait(false);
                    }

                    StorageTelemetry.RecordBytesWritten(preparedSource.Length);
                    _Logging.Info(_Header + "uploaded file '" + key + "' to S3");
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception uploading file '" + key + "':" + Environment.NewLine + ex.ToString());
                    throw;
                }
                finally
                {
                    preparedSource?.Dispose();
                }
            });
        }

        /// <inheritdoc />
        public Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("get_object", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

                string key = slug + "/" + Uri.EscapeDataString(fileName);
                _Logging.Debug(_Header + "downloading file '" + key + "' from S3");

                try
                {
                    GetObjectRequest request = new GetObjectRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = key
                    };

                    GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false);

                    string resolvedContentType = !String.IsNullOrEmpty(response.Headers.ContentType)
                        ? response.Headers.ContentType
                        : Constants.BinaryContentType;

                    StorageTelemetry.RecordBytesRead(response.ContentLength);
                    _Logging.Info(_Header + "downloading file '" + key + "' (" + response.ContentLength + " bytes)");

                    return new DownloadFileResult
                    {
                        Content = response.ResponseStream,
                        ContentType = resolvedContentType,
                        ContentLength = response.ContentLength,
                        FileName = fileName
                    };
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _Logging.Warn(_Header + "file '" + key + "' not found in S3");
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception downloading file '" + key + "':" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("delete_object", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

                string key = slug + "/" + Uri.EscapeDataString(fileName);
                _Logging.Debug(_Header + "deleting file '" + key + "' from S3");

                try
                {
                    DeleteObjectRequest request = new DeleteObjectRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = key
                    };

                    await _S3Client.DeleteObjectAsync(request, token).ConfigureAwait(false);

                    _Logging.Info(_Header + "deleted file '" + key + "' from S3");
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception deleting file '" + key + "':" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("batch_delete_objects", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (fileNames == null || fileNames.Count == 0) throw new ArgumentException("At least one filename is required.", nameof(fileNames));

                List<KeyVersion> keys = fileNames
                    .Select(f => new KeyVersion { Key = slug + "/" + Uri.EscapeDataString(f) })
                    .ToList();

                _Logging.Debug(_Header + "batch deleting " + keys.Count + " files from slug '" + slug + "'");

                DeleteObjectsRequest deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _S3Settings.Bucket,
                    Objects = keys
                };

                await _S3Client.DeleteObjectsAsync(deleteRequest, token).ConfigureAwait(false);

                _Logging.Info(_Header + "batch deleted " + keys.Count + " files from slug '" + slug + "'");
            });
        }

        /// <inheritdoc />
        public Task DeletePrefixAsync(string slug, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("delete_prefix", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

                string prefix = slug + "/";
                _Logging.Debug(_Header + "deleting all objects under prefix '" + prefix + "'");

                int totalDeleted = 0;
                string continuationToken = null;

                do
                {
                    ListObjectsV2Request listRequest = new ListObjectsV2Request
                    {
                        BucketName = _S3Settings.Bucket,
                        Prefix = prefix,
                        ContinuationToken = continuationToken
                    };

                    ListObjectsV2Response listResponse = await _S3Client.ListObjectsV2Async(listRequest, token).ConfigureAwait(false);

                    if (listResponse.S3Objects != null && listResponse.S3Objects.Count > 0)
                    {
                        DeleteObjectsRequest deleteRequest = new DeleteObjectsRequest
                        {
                            BucketName = _S3Settings.Bucket,
                            Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                        };

                        await _S3Client.DeleteObjectsAsync(deleteRequest, token).ConfigureAwait(false);
                        totalDeleted += listResponse.S3Objects.Count;
                    }

                    continuationToken = (listResponse.IsTruncated == true) ? listResponse.NextContinuationToken : null;

                } while (continuationToken != null);

                _Logging.Info(_Header + "deleted " + totalDeleted + " objects under prefix '" + prefix + "'");
            });
        }

        /// <inheritdoc />
        public Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("head_object", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

                string key = slug + "/" + Uri.EscapeDataString(fileName);
                _Logging.Debug(_Header + "getting metadata for file '" + key + "'");

                try
                {
                    GetObjectMetadataRequest request = new GetObjectMetadataRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = key
                    };

                    GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);

                    CollectionFile collectionFile = new CollectionFile
                    {
                        Key = key,
                        FileName = fileName,
                        Size = response.ContentLength,
                        LastModifiedUtc = response.LastModified.HasValue ? response.LastModified.Value.ToUniversalTime() : DateTime.UtcNow,
                        ContentType = !String.IsNullOrEmpty(response.Headers.ContentType) ? response.Headers.ContentType : Constants.BinaryContentType,
                        ETag = response.ETag
                    };

                    return collectionFile;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _Logging.Warn(_Header + "file '" + key + "' not found in S3");
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "exception getting metadata for file '" + key + "':" + Environment.NewLine + ex.ToString());
                    throw;
                }
            });
        }

        /// <inheritdoc />
        public Task<bool> FileExistsAsync(string slug, string fileName, CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("head_object_exists", async () =>
            {
                if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));
                if (String.IsNullOrEmpty(fileName)) throw new ArgumentNullException(nameof(fileName));

                string key = slug + "/" + Uri.EscapeDataString(fileName);

                try
                {
                    GetObjectMetadataRequest request = new GetObjectMetadataRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = key
                    };

                    await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);
                    return true;
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public Task EnsureCollectionsConfigExistsAsync(CancellationToken token = default)
        {
            return StorageTelemetry.MeasureAsync("ensure_config", async () =>
            {
                _Logging.Debug(_Header + "ensuring collections configuration exists in S3");

                try
                {
                    GetObjectMetadataRequest request = new GetObjectMetadataRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = Constants.CollectionsConfigKey
                    };

                    await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);
                    _Logging.Info(_Header + "collections configuration already exists in S3");
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _Logging.Info(_Header + "collections configuration not found, creating empty configuration");

                    string json = _Serializer.SerializeJson(new List<Collection>(), true);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);

                    PutObjectRequest putRequest = new PutObjectRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = Constants.CollectionsConfigKey,
                        ContentType = Constants.JsonContentType,
                        InputStream = new MemoryStream(bytes)
                    };
                    ApplyCompatibilityMetadata(putRequest);

                    await _S3Client.PutObjectAsync(putRequest, token).ConfigureAwait(false);
                    StorageTelemetry.RecordBytesWritten(bytes.Length);
                    _Logging.Info(_Header + "created empty collections configuration in S3");
                }
            });
        }

        #endregion

        #region Private-Methods

        private static IS3ClientAdapter CreateClientAdapter(S3Settings settings)
        {
            Amazon.S3.AmazonS3Config config = new Amazon.S3.AmazonS3Config();

            if (!String.IsNullOrEmpty(settings.EndpointUrl))
            {
                config.ServiceURL = settings.EndpointUrl;
                config.UseHttp = !settings.UseSsl;
            }
            else
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
            }

            if (settings.RequestStyle == Settings.S3RequestStyle.PathStyle)
            {
                config.ForcePathStyle = true;
            }

            return new AwsS3ClientAdapter(new AmazonS3Client(settings.AccessKey, settings.SecretKey, config));
        }

        private async Task<PreparedUploadSource> PrepareUploadSourceAsync(Stream content, CancellationToken token)
        {
            if (content.CanSeek)
            {
                return new PreparedUploadSource(content, content.Position, content.Length - content.Position, ownsStream: false);
            }

            string tempPath = Path.Combine(Path.GetTempPath(), "mincms-s3-upload-" + Guid.NewGuid().ToString("N") + ".tmp");
            FileStream readStream = null;

            try
            {
                using (FileStream writeStream = new FileStream(
                    tempPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    65536,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await content.CopyToAsync(writeStream, token).ConfigureAwait(false);
                    await writeStream.FlushAsync(token).ConfigureAwait(false);
                }

                readStream = new FileStream(
                    tempPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    65536,
                    FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);

                return new PreparedUploadSource(readStream, 0, readStream.Length, ownsStream: true);
            }
            catch
            {
                readStream?.Dispose();

                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                throw;
            }
        }

        private bool ShouldUseMultipartUpload(long contentLength)
        {
            return contentLength >= _S3Settings.MultipartThresholdBytes;
        }

        private async Task UploadFileMultipartAsync(string key, PreparedUploadSource source, string contentType, CancellationToken token)
        {
            InitiateMultipartUploadResponse initiateResponse = null;

            try
            {
                InitiateMultipartUploadRequest initiateRequest = new InitiateMultipartUploadRequest
                {
                    BucketName = _S3Settings.Bucket,
                    Key = key,
                    ContentType = contentType
                };
                ApplyCompatibilityMetadata(initiateRequest);

                initiateResponse = await _S3Client.InitiateMultipartUploadAsync(initiateRequest, token).ConfigureAwait(false);

                List<PartETag> partEtags = new List<PartETag>();
                long offset = source.StartPosition;
                long remaining = source.Length;
                int partNumber = 1;

                while (remaining > 0)
                {
                    long partLength = Math.Min(_S3Settings.MultipartPartSizeBytes, remaining);

                    using ReadOnlySubStream partStream = new ReadOnlySubStream(source.Stream, offset, partLength, leaveOpen: true);
                    UploadPartRequest uploadPartRequest = new UploadPartRequest
                    {
                        BucketName = _S3Settings.Bucket,
                        Key = key,
                        UploadId = initiateResponse.UploadId,
                        PartNumber = partNumber,
                        PartSize = partLength,
                        InputStream = partStream
                    };

                    UploadPartResponse uploadPartResponse = await _S3Client.UploadPartAsync(uploadPartRequest, token).ConfigureAwait(false);
                    partEtags.Add(new PartETag(partNumber, uploadPartResponse.ETag));

                    offset += partLength;
                    remaining -= partLength;
                    partNumber++;
                }

                CompleteMultipartUploadRequest completeRequest = new CompleteMultipartUploadRequest
                {
                    BucketName = _S3Settings.Bucket,
                    Key = key,
                    UploadId = initiateResponse.UploadId,
                    PartETags = partEtags
                };

                await _S3Client.CompleteMultipartUploadAsync(completeRequest, token).ConfigureAwait(false);
            }
            catch
            {
                if (!String.IsNullOrEmpty(initiateResponse?.UploadId))
                {
                    try
                    {
                        AbortMultipartUploadRequest abortRequest = new AbortMultipartUploadRequest
                        {
                            BucketName = _S3Settings.Bucket,
                            Key = key,
                            UploadId = initiateResponse.UploadId
                        };

                        await _S3Client.AbortMultipartUploadAsync(abortRequest, token).ConfigureAwait(false);
                    }
                    catch (Exception abortException)
                    {
                        _Logging.Warn(_Header + "exception aborting multipart upload '" + key + "':" + Environment.NewLine + abortException.ToString());
                    }
                }

                throw;
            }
        }

        private void ApplyCompatibilityMetadata(PutObjectRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            // Less3 currently fails object reads when metadata is persisted as an empty string.
            // Always sending one user-metadata entry keeps the stored value valid JSON.
            request.Metadata["mincms-origin"] = "mincms";
        }

        private void ApplyCompatibilityMetadata(InitiateMultipartUploadRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            request.Metadata["mincms-origin"] = "mincms";
        }

        private sealed class PreparedUploadSource : IDisposable
        {
            public long Length { get; }

            public bool OwnsStream { get; }

            public long StartPosition { get; }

            public Stream Stream { get; }

            public PreparedUploadSource(Stream stream, long startPosition, long length, bool ownsStream)
            {
                Stream = stream ?? throw new ArgumentNullException(nameof(stream));
                StartPosition = startPosition;
                Length = length;
                OwnsStream = ownsStream;
            }

            public void Dispose()
            {
                if (OwnsStream)
                {
                    Stream.Dispose();
                }
            }
        }

        #endregion
    }
}
