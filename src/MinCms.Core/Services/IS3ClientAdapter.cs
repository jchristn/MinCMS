namespace MinCms.Core.Services
{
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.S3.Model;

    /// <summary>
    /// Narrow adapter over the AWS S3 client used by MinCMS.
    /// </summary>
    public interface IS3ClientAdapter
    {
        Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken token = default);

        Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken token = default);

        Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken token = default);

        Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken token = default);

        Task<DeleteObjectResponse> DeleteObjectAsync(DeleteObjectRequest request, CancellationToken token = default);

        Task<DeleteObjectsResponse> DeleteObjectsAsync(DeleteObjectsRequest request, CancellationToken token = default);

        Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request, CancellationToken token = default);

        Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request, CancellationToken token = default);

        Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request, CancellationToken token = default);

        Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(AbortMultipartUploadRequest request, CancellationToken token = default);
    }
}
