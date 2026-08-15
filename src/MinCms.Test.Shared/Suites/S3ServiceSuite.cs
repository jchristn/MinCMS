namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using MinCms.Core;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>S3 storage service behavior against an in-memory fake S3 client.</summary>
        public static TestSuiteDescriptor S3ServiceSuite()
        {
            const string s = "S3Service";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                // ---- Constructor validation ----
                Case(s, "Ctor.NullSettings", "S3Service rejects null settings", () =>
                    Check.Throws<ArgumentNullException>(() => new S3Service(null, QuietLogging(), new FakeS3ClientAdapter()))),

                Case(s, "Ctor.NullLogging", "S3Service rejects null logging", () =>
                    Check.Throws<ArgumentNullException>(() => new S3Service(CreateS3Settings(), null, new FakeS3ClientAdapter()))),

                // ---- Upload path selection ----
                Case(s, "Upload.SmallUsesPutObject", "Small streams use PutObject with compatibility metadata", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    byte[] payload = CreatePayload(1024);

                    await service.UploadFileAsync("alpha", "small.txt", new MemoryStream(payload, writable: false), "text/plain");

                    Check.Equal(1, client.PutObjectCalls.Count);
                    Check.Equal(0, client.InitiateMultipartUploadCalls.Count);
                    Check.Equal("bucket", client.PutObjectCalls[0].BucketName);
                    Check.Equal("alpha/small.txt", client.PutObjectCalls[0].Key);
                    Check.Equal("text/plain", client.PutObjectCalls[0].ContentType);
                    Check.Equal("mincms", client.PutObjectCalls[0].OriginMetadata);
                    Check.BytesEqual(payload, client.PutObjectCalls[0].Body);
                }),

                Case(s, "Upload.LargeUsesMultipart", "Large seekable streams use multipart with correct part sizes", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    byte[] payload = CreatePayload((11 * 1024 * 1024) + 123);

                    await service.UploadFileAsync("alpha", "deck.pptx", new MemoryStream(payload, writable: false), "application/vnd.ms-powerpoint");

                    Check.Equal(0, client.PutObjectCalls.Count);
                    Check.Equal(1, client.InitiateMultipartUploadCalls.Count);
                    Check.Equal("mincms", client.InitiateMultipartUploadCalls[0].OriginMetadata);
                    Check.Equal(3, client.UploadPartCalls.Count);
                    Check.Equal(5L * 1024L * 1024L, client.UploadPartCalls[0].PartSize);
                    Check.Equal(5L * 1024L * 1024L, client.UploadPartCalls[1].PartSize);
                    Check.Equal((1L * 1024L * 1024L) + 123L, client.UploadPartCalls[2].PartSize);
                    Check.BytesEqual(payload, client.UploadPartCalls.SelectMany(x => x.Body).ToArray());
                    Check.Equal(1, client.CompleteMultipartUploadCalls.Count);
                    Check.True(client.CompleteMultipartUploadCalls[0].PartNumbers.SequenceEqual(new[] { 1, 2, 3 }), "Completed part numbers should be 1,2,3");
                }),

                Case(s, "Upload.LargeNonSeekableUsesMultipart", "Large non-seekable streams buffer to a temp file then multipart", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    byte[] payload = CreatePayload((6 * 1024 * 1024) + 17);

                    await service.UploadFileAsync("alpha", "stream.bin", new NonSeekableReadStream(payload), "application/octet-stream");

                    Check.Equal(0, client.PutObjectCalls.Count);
                    Check.Equal(1, client.InitiateMultipartUploadCalls.Count);
                    Check.Equal(2, client.UploadPartCalls.Count);
                    Check.BytesEqual(payload, client.UploadPartCalls.SelectMany(x => x.Body).ToArray());
                }),

                Case(s, "Upload.AbortsOnPartFailure", "A failed part aborts the multipart upload", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { FailPartNumber = 2 };
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    byte[] payload = CreatePayload((6 * 1024 * 1024) + 17);

                    await Check.ThrowsAsync<InvalidOperationException>(() =>
                        service.UploadFileAsync("alpha", "broken.bin", new MemoryStream(payload, writable: false), "application/octet-stream"));

                    Check.Equal(1, client.InitiateMultipartUploadCalls.Count);
                    Check.Equal(1, client.AbortMultipartUploadCalls.Count);
                    Check.Equal(0, client.CompleteMultipartUploadCalls.Count);
                    Check.Equal(client.InitiateMultipartUploadCalls[0].UploadId, client.AbortMultipartUploadCalls[0].UploadId);
                }),

                Case(s, "Upload.EmptyContentTypeDefaultsBinary", "Empty content type defaults to application/octet-stream", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);

                    await service.UploadFileAsync("alpha", "unknown.dat", new MemoryStream(CreatePayload(32)), "");

                    Check.Equal(Constants.BinaryContentType, client.PutObjectCalls[0].ContentType);
                }),

                Case(s, "Upload.EncodesKey", "Upload escapes file names into the object key and download round-trips", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);

                    await service.UploadFileAsync("alpha", "my file.txt", new MemoryStream(Encoding.UTF8.GetBytes("hi")), "text/plain");
                    Check.Equal("alpha/my%20file.txt", client.PutObjectCalls[0].Key);

                    List<CollectionFile> files = await service.ListFilesAsync("alpha");
                    Check.True(files.Any(f => f.FileName == "my file.txt"), "Listing should unescape the file name");
                }),

                // ---- Collections config ----
                Case(s, "Collections.LoadExisting", "LoadCollectionsAsync parses the stored config", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    MinCms.Core.Serialization.Serializer serializer = new MinCms.Core.Serialization.Serializer();
                    List<Collection> seed = new List<Collection> { new Collection("Alpha", "alpha"), new Collection("Beta", "beta") };
                    client.Seed(Constants.CollectionsConfigKey, Encoding.UTF8.GetBytes(serializer.SerializeJson(seed, false)), Constants.JsonContentType);

                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    List<Collection> loaded = await service.LoadCollectionsAsync();

                    Check.Equal(2, loaded.Count);
                    Check.True(loaded.Any(c => c.Slug == "beta"), "Should contain beta");
                }),

                Case(s, "Collections.LoadMissingReturnsEmpty", "LoadCollectionsAsync returns an empty list when config is absent", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    List<Collection> loaded = await service.LoadCollectionsAsync();
                    Check.Equal(0, loaded.Count);
                }),

                Case(s, "Collections.SaveThenReload", "SaveCollectionsAsync persists a config that reloads", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);

                    await service.SaveCollectionsAsync(new List<Collection> { new Collection("Alpha", "alpha") }, null);
                    List<Collection> loaded = await service.LoadCollectionsAsync();

                    Check.Equal(1, loaded.Count);
                    Check.Equal("alpha", loaded[0].Slug);
                }),

                Case(s, "Collections.SaveNull", "SaveCollectionsAsync rejects a null list", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.SaveCollectionsAsync(null, null));
                }),

                Case(s, "Collections.SavePreconditionFailed", "A precondition failure surfaces as a conflict", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { FailConfigSaveWithPreconditionFailed = true };
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await Check.ThrowsAsync<InvalidOperationException>(() =>
                        service.SaveCollectionsAsync(new List<Collection> { new Collection("Alpha", "alpha") }, "etag"));
                }),

                Case(s, "Collections.ETagPresentAndMissing", "GetCollectionsETagAsync returns an etag or null", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    Check.Null(await service.GetCollectionsETagAsync());

                    client.Seed(Constants.CollectionsConfigKey, Encoding.UTF8.GetBytes("[]"), Constants.JsonContentType);
                    Check.NotNull(await service.GetCollectionsETagAsync());
                }),

                Case(s, "Collections.EnsureConfigCreatesWhenMissing", "EnsureCollectionsConfigExistsAsync creates an empty config only when missing", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);

                    await service.EnsureCollectionsConfigExistsAsync();
                    Check.True(client.Contains(Constants.CollectionsConfigKey), "Config should be created");
                    Check.Equal(1, client.PutObjectCalls.Count);

                    await service.EnsureCollectionsConfigExistsAsync();
                    Check.Equal(1, client.PutObjectCalls.Count, "Existing config should not be rewritten");
                }),

                // ---- File operations ----
                Case(s, "Files.ListWithPagination", "ListFilesAsync follows continuation tokens", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { PageSize = 2 };
                    for (int i = 0; i < 5; i++)
                        client.Seed("alpha/file" + i + ".txt", CreatePayload(10), "text/plain");
                    client.Seed("beta/other.txt", CreatePayload(10), "text/plain");

                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    List<CollectionFile> files = await service.ListFilesAsync("alpha");

                    Check.Equal(5, files.Count);
                    Check.True(files.All(f => f.Key.StartsWith("alpha/", StringComparison.Ordinal)), "Only alpha files should be returned");
                }),

                Case(s, "Files.DownloadReturnsContent", "DownloadFileAsync returns stored content and type", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await service.UploadFileAsync("alpha", "note.txt", new MemoryStream(Encoding.UTF8.GetBytes("hello")), "text/plain");

                    DownloadFileResult result = await service.DownloadFileAsync("alpha", "note.txt");
                    using StreamReader reader = new StreamReader(result.Content);
                    Check.Equal("hello", reader.ReadToEnd());
                    Check.Equal("text/plain", result.ContentType);
                    Check.Equal("note.txt", result.FileName);
                }),

                Case(s, "Files.DownloadMissing", "DownloadFileAsync throws KeyNotFound for a missing file", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.DownloadFileAsync("alpha", "ghost.txt"));
                }),

                Case(s, "Files.MetadataMissing", "GetFileMetadataAsync throws KeyNotFound for a missing file", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.GetFileMetadataAsync("alpha", "ghost.txt"));
                }),

                Case(s, "Files.ExistsTrueFalse", "FileExistsAsync reports presence", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    Check.False(await service.FileExistsAsync("alpha", "note.txt"));
                    await service.UploadFileAsync("alpha", "note.txt", new MemoryStream(Encoding.UTF8.GetBytes("x")), "text/plain");
                    Check.True(await service.FileExistsAsync("alpha", "note.txt"));
                }),

                Case(s, "Files.DeleteSingle", "DeleteFileAsync removes an object", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await service.UploadFileAsync("alpha", "note.txt", new MemoryStream(Encoding.UTF8.GetBytes("x")), "text/plain");

                    await service.DeleteFileAsync("alpha", "note.txt");
                    Check.False(await service.FileExistsAsync("alpha", "note.txt"));
                }),

                Case(s, "Files.DeleteBatch", "DeleteFilesAsync removes multiple objects and rejects empty input", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await service.UploadFileAsync("alpha", "a.txt", new MemoryStream(CreatePayload(4)), "text/plain");
                    await service.UploadFileAsync("alpha", "b.txt", new MemoryStream(CreatePayload(4)), "text/plain");

                    await service.DeleteFilesAsync("alpha", new List<string> { "a.txt", "b.txt" });
                    Check.Equal(0, (await service.ListFilesAsync("alpha")).Count);

                    await Check.ThrowsAsync<ArgumentException>(() => service.DeleteFilesAsync("alpha", new List<string>()));
                }),

                Case(s, "Files.DeletePrefix", "DeletePrefixAsync removes every object under the prefix", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { PageSize = 2 };
                    for (int i = 0; i < 5; i++)
                        client.Seed("alpha/file" + i + ".txt", CreatePayload(4), "text/plain");
                    client.Seed("beta/keep.txt", CreatePayload(4), "text/plain");

                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    await service.DeletePrefixAsync("alpha");

                    Check.False(client.Contains("alpha/file0.txt"), "alpha objects should be gone");
                    Check.True(client.Contains("beta/keep.txt"), "beta objects should remain");
                })
            };

            return new TestSuiteDescriptor(s, "S3 storage service", cases);
        }

        private static S3Settings CreateS3Settings()
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
    }
}
