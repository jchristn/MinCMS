namespace MinCms.Server.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3.Model;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using SyslogLogging;
    using Xunit;

    public class S3ServiceTests
    {
        [Fact]
        public async Task UploadFileAsync_UsesPutObject_ForSmallStreams()
        {
            FakeS3ClientAdapter client = new FakeS3ClientAdapter();
            S3Service service = new S3Service(CreateSettings(), new LoggingModule(), client);
            byte[] payload = CreatePayload(1024);

            await service.UploadFileAsync("alpha", "small.txt", new MemoryStream(payload, writable: false), "text/plain");

            Assert.Single(client.PutObjectCalls);
            Assert.Empty(client.InitiateMultipartUploadCalls);
            Assert.Equal("bucket", client.PutObjectCalls[0].BucketName);
            Assert.Equal("alpha/small.txt", client.PutObjectCalls[0].Key);
            Assert.Equal("text/plain", client.PutObjectCalls[0].ContentType);
            Assert.Equal("mincms", client.PutObjectCalls[0].OriginMetadata);
            Assert.Equal(payload, client.PutObjectCalls[0].Body);
        }

        [Fact]
        public async Task UploadFileAsync_UsesMultipart_ForLargeStreams()
        {
            FakeS3ClientAdapter client = new FakeS3ClientAdapter();
            S3Service service = new S3Service(CreateSettings(), new LoggingModule(), client);
            byte[] payload = CreatePayload((11 * 1024 * 1024) + 123);

            await service.UploadFileAsync("alpha", "deck.pptx", new MemoryStream(payload, writable: false), "application/vnd.ms-powerpoint");

            Assert.Empty(client.PutObjectCalls);
            Assert.Single(client.InitiateMultipartUploadCalls);
            Assert.Equal("mincms", client.InitiateMultipartUploadCalls[0].OriginMetadata);
            Assert.Equal(3, client.UploadPartCalls.Count);
            Assert.Equal(5L * 1024L * 1024L, client.UploadPartCalls[0].PartSize);
            Assert.Equal(5L * 1024L * 1024L, client.UploadPartCalls[1].PartSize);
            Assert.Equal((1L * 1024L * 1024L) + 123L, client.UploadPartCalls[2].PartSize);
            Assert.Equal(payload.AsEnumerable(), client.UploadPartCalls.SelectMany(x => x.Body));
            Assert.Single(client.CompleteMultipartUploadCalls);
            Assert.Equal(new[] { 1, 2, 3 }, client.CompleteMultipartUploadCalls[0].PartNumbers);
        }

        [Fact]
        public async Task UploadFileAsync_UsesMultipart_ForLargeNonSeekableStreams()
        {
            FakeS3ClientAdapter client = new FakeS3ClientAdapter();
            S3Service service = new S3Service(CreateSettings(), new LoggingModule(), client);
            byte[] payload = CreatePayload((6 * 1024 * 1024) + 17);

            await service.UploadFileAsync("alpha", "stream.bin", new NonSeekableReadStream(payload), "application/octet-stream");

            Assert.Empty(client.PutObjectCalls);
            Assert.Single(client.InitiateMultipartUploadCalls);
            Assert.Equal(2, client.UploadPartCalls.Count);
            Assert.Equal(payload.AsEnumerable(), client.UploadPartCalls.SelectMany(x => x.Body));
        }

        [Fact]
        public async Task UploadFileAsync_AbortsMultipart_WhenPartUploadFails()
        {
            FakeS3ClientAdapter client = new FakeS3ClientAdapter
            {
                FailPartNumber = 2
            };
            S3Service service = new S3Service(CreateSettings(), new LoggingModule(), client);
            byte[] payload = CreatePayload((6 * 1024 * 1024) + 17);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.UploadFileAsync("alpha", "broken.bin", new MemoryStream(payload, writable: false), "application/octet-stream"));

            Assert.Single(client.InitiateMultipartUploadCalls);
            Assert.Single(client.AbortMultipartUploadCalls);
            Assert.Empty(client.CompleteMultipartUploadCalls);
            Assert.Equal(client.UploadId, client.AbortMultipartUploadCalls[0].UploadId);
        }

        private static S3Settings CreateSettings()
        {
            return new S3Settings
            {
                AccessKey = "key",
                SecretKey = "secret",
                Bucket = "bucket",
                Region = "us-west-2",
                MultipartThresholdBytes = 5L * 1024L * 1024L,
                MultipartPartSizeBytes = 5L * 1024L * 1024L
            };
        }

        private static byte[] CreatePayload(int length)
        {
            byte[] data = new byte[length];

            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 251);
            }

            return data;
        }

        private sealed class FakeS3ClientAdapter : IS3ClientAdapter
        {
            public List<AbortMultipartUploadCall> AbortMultipartUploadCalls { get; } = new List<AbortMultipartUploadCall>();

            public List<CompleteMultipartUploadCall> CompleteMultipartUploadCalls { get; } = new List<CompleteMultipartUploadCall>();

            public int FailPartNumber { get; set; } = -1;

            public List<InitiateMultipartUploadCall> InitiateMultipartUploadCalls { get; } = new List<InitiateMultipartUploadCall>();

            public List<PutObjectCall> PutObjectCalls { get; } = new List<PutObjectCall>();

            public List<UploadPartCall> UploadPartCalls { get; } = new List<UploadPartCall>();

            public string UploadId { get; } = "upload-123";

            public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken token = default)
            {
                AbortMultipartUploadCalls.Add(new AbortMultipartUploadCall(request.UploadId));
                return Task.FromResult(new AbortMultipartUploadResponse());
            }

            public Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken token = default)
            {
                CompleteMultipartUploadCalls.Add(new CompleteMultipartUploadCall(
                    request.UploadId,
                    request.PartETags?.Select(x => x.PartNumber ?? 0).ToList() ?? new List<int>()));
                return Task.FromResult(new CompleteMultipartUploadResponse());
            }

            public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken token = default)
            {
                InitiateMultipartUploadCalls.Add(new InitiateMultipartUploadCall(request.BucketName, request.Key, request.ContentType, request.Metadata["mincms-origin"]));
                return Task.FromResult(new InitiateMultipartUploadResponse { UploadId = UploadId });
            }

            public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken token = default)
            {
                throw new NotSupportedException();
            }

            public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken token = default)
            {
                PutObjectCalls.Add(new PutObjectCall(
                    request.BucketName,
                    request.Key,
                    request.ContentType,
                    request.Metadata["mincms-origin"],
                    await ReadAllBytesAsync(request.InputStream, token).ConfigureAwait(false)));

                return new PutObjectResponse();
            }

            public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken token = default)
            {
                if (request.PartNumber == FailPartNumber)
                {
                    throw new InvalidOperationException("Simulated multipart upload failure.");
                }

                UploadPartCalls.Add(new UploadPartCall(
                    request.UploadId,
                    request.PartNumber ?? 0,
                    request.PartSize ?? 0,
                    await ReadAllBytesAsync(request.InputStream, token).ConfigureAwait(false)));

                return new UploadPartResponse
                {
                    ETag = "\"etag-" + request.PartNumber + "\""
                };
            }

            private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken token)
            {
                using MemoryStream ms = new MemoryStream();
                await stream.CopyToAsync(ms, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        private sealed record AbortMultipartUploadCall(string UploadId);

        private sealed record CompleteMultipartUploadCall(string UploadId, List<int> PartNumbers);

        private sealed record InitiateMultipartUploadCall(string BucketName, string Key, string ContentType, string OriginMetadata);

        private sealed record PutObjectCall(string BucketName, string Key, string ContentType, string OriginMetadata, byte[] Body);

        private sealed record UploadPartCall(string UploadId, int PartNumber, long PartSize, byte[] Body);
    }
}
