namespace MinCms.Test.Shared.Support
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using MinCms.Core.Services;

    /// <summary>
    /// In-memory implementation of <see cref="IS3ClientAdapter"/> that both stores objects
    /// (so <c>S3Service</c> can be exercised end to end) and records the calls it receives
    /// (so multipart mechanics can be asserted).
    /// </summary>
    public sealed class FakeS3ClientAdapter : IS3ClientAdapter
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<string, StoredObject> _Store = new Dictionary<string, StoredObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, MultipartUpload> _Uploads = new Dictionary<string, MultipartUpload>(StringComparer.Ordinal);
        private int _UploadCounter;

        /// <summary>Recorded PutObject calls.</summary>
        public List<PutObjectCall> PutObjectCalls { get; } = new List<PutObjectCall>();

        /// <summary>Recorded InitiateMultipartUpload calls.</summary>
        public List<InitiateMultipartUploadCall> InitiateMultipartUploadCalls { get; } = new List<InitiateMultipartUploadCall>();

        /// <summary>Recorded UploadPart calls.</summary>
        public List<UploadPartCall> UploadPartCalls { get; } = new List<UploadPartCall>();

        /// <summary>Recorded CompleteMultipartUpload calls.</summary>
        public List<CompleteMultipartUploadCall> CompleteMultipartUploadCalls { get; } = new List<CompleteMultipartUploadCall>();

        /// <summary>Recorded AbortMultipartUpload calls.</summary>
        public List<AbortMultipartUploadCall> AbortMultipartUploadCalls { get; } = new List<AbortMultipartUploadCall>();

        /// <summary>Recorded single-object delete calls.</summary>
        public List<string> DeleteObjectCalls { get; } = new List<string>();

        /// <summary>Recorded batch delete calls (list of key lists).</summary>
        public List<List<string>> DeleteObjectsCalls { get; } = new List<List<string>>();

        /// <summary>When set to a part number, that UploadPart call throws to simulate a failure.</summary>
        public int FailPartNumber { get; set; } = -1;

        /// <summary>When true, PutObject against the collections config key throws a PreconditionFailed error.</summary>
        public bool FailConfigSaveWithPreconditionFailed { get; set; }

        /// <summary>When greater than zero, ListObjectsV2 returns at most this many keys per page.</summary>
        public int PageSize { get; set; }

        /// <summary>Number of objects currently stored.</summary>
        public int StoredObjectCount
        {
            get { lock (_Sync) { return _Store.Count; } }
        }

        /// <summary>True if an object with the given key is stored.</summary>
        public bool Contains(string key)
        {
            lock (_Sync) { return _Store.ContainsKey(key); }
        }

        /// <summary>Directly seed a stored object (bypasses recording).</summary>
        public void Seed(string key, byte[] body, string contentType)
        {
            lock (_Sync)
            {
                _Store[key] = new StoredObject
                {
                    Body = body ?? Array.Empty<byte>(),
                    ContentType = contentType,
                    LastModified = DateTime.UtcNow,
                    ETag = NewETag()
                };
            }
        }

        /// <inheritdoc />
        public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Store.TryGetValue(request.Key, out StoredObject stored))
                    throw NotFound(request.Key);

                GetObjectResponse response = new GetObjectResponse
                {
                    ResponseStream = new MemoryStream(stored.Body, writable: false),
                    ContentLength = stored.Body.LongLength,
                    ETag = stored.ETag
                };
                response.Headers.ContentType = stored.ContentType;
                return Task.FromResult(response);
            }
        }

        /// <inheritdoc />
        public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Store.TryGetValue(request.Key, out StoredObject stored))
                    throw NotFound(request.Key);

                GetObjectMetadataResponse response = new GetObjectMetadataResponse
                {
                    ContentLength = stored.Body.LongLength,
                    ETag = stored.ETag,
                    LastModified = stored.LastModified
                };
                response.Headers.ContentType = stored.ContentType;
                return Task.FromResult(response);
            }
        }

        /// <inheritdoc />
        public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken token = default)
        {
            byte[] body = await ReadAllBytesAsync(request.InputStream, token).ConfigureAwait(false);
            string origin = request.Metadata["mincms-origin"];

            lock (_Sync)
            {
                if (FailConfigSaveWithPreconditionFailed
                    && String.Equals(request.Key, MinCms.Core.Constants.CollectionsConfigKey, StringComparison.Ordinal))
                {
                    throw PreconditionFailed(request.Key);
                }

                PutObjectCalls.Add(new PutObjectCall(request.BucketName, request.Key, request.ContentType, origin, body));

                _Store[request.Key] = new StoredObject
                {
                    Body = body,
                    ContentType = request.ContentType,
                    LastModified = DateTime.UtcNow,
                    ETag = NewETag()
                };
            }

            return new PutObjectResponse { ETag = _Store[request.Key].ETag };
        }

        /// <inheritdoc />
        public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                string prefix = request.Prefix ?? String.Empty;
                List<string> matchingKeys = _Store.Keys
                    .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToList();

                int start = 0;
                if (!String.IsNullOrEmpty(request.ContinuationToken))
                    Int32.TryParse(request.ContinuationToken, out start);

                IEnumerable<string> pageKeys = matchingKeys.Skip(start);
                bool truncated = false;
                string nextToken = null;

                if (PageSize > 0)
                {
                    pageKeys = pageKeys.Take(PageSize);
                    int nextStart = start + PageSize;
                    if (nextStart < matchingKeys.Count)
                    {
                        truncated = true;
                        nextToken = nextStart.ToString();
                    }
                }

                List<S3Object> objects = pageKeys.Select(k => new S3Object
                {
                    Key = k,
                    Size = _Store[k].Body.LongLength,
                    LastModified = _Store[k].LastModified,
                    ETag = _Store[k].ETag
                }).ToList();

                return Task.FromResult(new ListObjectsV2Response
                {
                    S3Objects = objects,
                    IsTruncated = truncated,
                    NextContinuationToken = nextToken
                });
            }
        }

        /// <inheritdoc />
        public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                DeleteObjectCalls.Add(request.Key);
                _Store.Remove(request.Key);
                return Task.FromResult(new DeleteObjectResponse());
            }
        }

        /// <inheritdoc />
        public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                List<string> keys = (request.Objects ?? new List<KeyVersion>()).Select(o => o.Key).ToList();
                DeleteObjectsCalls.Add(keys);

                foreach (string key in keys)
                    _Store.Remove(key);

                return Task.FromResult(new DeleteObjectsResponse());
            }
        }

        /// <inheritdoc />
        public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                string uploadId = "upload-" + (++_UploadCounter);
                _Uploads[uploadId] = new MultipartUpload
                {
                    Key = request.Key,
                    ContentType = request.ContentType
                };

                InitiateMultipartUploadCalls.Add(new InitiateMultipartUploadCall(
                    request.BucketName, request.Key, request.ContentType, request.Metadata["mincms-origin"], uploadId));

                return Task.FromResult(new InitiateMultipartUploadResponse { UploadId = uploadId });
            }
        }

        /// <inheritdoc />
        public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken token = default)
        {
            int partNumber = request.PartNumber ?? 0;
            if (partNumber == FailPartNumber)
                throw new InvalidOperationException("Simulated multipart upload failure.");

            byte[] body = await ReadAllBytesAsync(request.InputStream, token).ConfigureAwait(false);

            lock (_Sync)
            {
                UploadPartCalls.Add(new UploadPartCall(request.UploadId, partNumber, request.PartSize ?? 0, body));

                if (_Uploads.TryGetValue(request.UploadId, out MultipartUpload upload))
                    upload.Parts[partNumber] = body;
            }

            return new UploadPartResponse { ETag = "\"etag-" + partNumber + "\"" };
        }

        /// <inheritdoc />
        public Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                List<int> partNumbers = request.PartETags?.Select(x => x.PartNumber ?? 0).ToList() ?? new List<int>();
                CompleteMultipartUploadCalls.Add(new CompleteMultipartUploadCall(request.UploadId, partNumbers));

                if (_Uploads.TryGetValue(request.UploadId, out MultipartUpload upload))
                {
                    byte[] assembled = upload.Parts.OrderBy(kvp => kvp.Key).SelectMany(kvp => kvp.Value).ToArray();
                    _Store[upload.Key] = new StoredObject
                    {
                        Body = assembled,
                        ContentType = upload.ContentType,
                        LastModified = DateTime.UtcNow,
                        ETag = NewETag()
                    };
                    _Uploads.Remove(request.UploadId);
                }

                return Task.FromResult(new CompleteMultipartUploadResponse());
            }
        }

        /// <inheritdoc />
        public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken token = default)
        {
            lock (_Sync)
            {
                AbortMultipartUploadCalls.Add(new AbortMultipartUploadCall(request.UploadId));
                _Uploads.Remove(request.UploadId);
                return Task.FromResult(new AbortMultipartUploadResponse());
            }
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken token)
        {
            using MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms, token).ConfigureAwait(false);
            return ms.ToArray();
        }

        private static AmazonS3Exception NotFound(string key)
        {
            return new AmazonS3Exception("The specified key does not exist: " + key, ErrorType.Sender, "NoSuchKey", "fake-request", HttpStatusCode.NotFound);
        }

        private static AmazonS3Exception PreconditionFailed(string key)
        {
            return new AmazonS3Exception("Precondition failed for key: " + key, ErrorType.Sender, "PreconditionFailed", "fake-request", HttpStatusCode.PreconditionFailed);
        }

        private string NewETag() => "\"" + Guid.NewGuid().ToString("N") + "\"";

        private sealed class StoredObject
        {
            public byte[] Body { get; set; } = Array.Empty<byte>();
            public string ContentType { get; set; }
            public DateTime LastModified { get; set; } = DateTime.UtcNow;
            public string ETag { get; set; }
        }

        private sealed class MultipartUpload
        {
            public string Key { get; set; }
            public string ContentType { get; set; }
            public Dictionary<int, byte[]> Parts { get; } = new Dictionary<int, byte[]>();
        }

        /// <summary>Recorded PutObject call.</summary>
        public sealed record PutObjectCall(string BucketName, string Key, string ContentType, string OriginMetadata, byte[] Body);

        /// <summary>Recorded InitiateMultipartUpload call.</summary>
        public sealed record InitiateMultipartUploadCall(string BucketName, string Key, string ContentType, string OriginMetadata, string UploadId);

        /// <summary>Recorded UploadPart call.</summary>
        public sealed record UploadPartCall(string UploadId, int PartNumber, long PartSize, byte[] Body);

        /// <summary>Recorded CompleteMultipartUpload call.</summary>
        public sealed record CompleteMultipartUploadCall(string UploadId, List<int> PartNumbers);

        /// <summary>Recorded AbortMultipartUpload call.</summary>
        public sealed record AbortMultipartUploadCall(string UploadId);
    }
}
