namespace MinCms.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3;
    using Amazon.S3.Model;

    /// <summary>
    /// Default AWS SDK-backed implementation of <see cref="IS3ClientAdapter"/>.
    /// </summary>
    internal sealed class AwsS3ClientAdapter : IS3ClientAdapter
    {
        private readonly IAmazonS3 _Client;

        public AwsS3ClientAdapter(IAmazonS3 client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken token = default)
        {
            return _Client.GetObjectAsync(request, token);
        }

        public Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken token = default)
        {
            return _Client.PutObjectAsync(request, token);
        }

        public Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken token = default)
        {
            return _Client.GetObjectMetadataAsync(request, token);
        }

        public Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken token = default)
        {
            return _Client.ListObjectsV2Async(request, token);
        }

        public Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken token = default)
        {
            return _Client.DeleteObjectAsync(request, token);
        }

        public Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken token = default)
        {
            return _Client.DeleteObjectsAsync(request, token);
        }

        public Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken token = default)
        {
            return _Client.InitiateMultipartUploadAsync(request, token);
        }

        public Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken token = default)
        {
            return _Client.UploadPartAsync(request, token);
        }

        public Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken token = default)
        {
            return _Client.CompleteMultipartUploadAsync(request, token);
        }

        public Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken token = default)
        {
            return _Client.AbortMultipartUploadAsync(request, token);
        }
    }
}
