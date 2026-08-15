namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using MinCms.Core;
    using MinCms.Core.Services;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>Collection management service behavior over the S3 service.</summary>
        public static TestSuiteDescriptor CollectionServiceSuite()
        {
            const string s = "CollectionService";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(s, "Ctor.NullS3", "CollectionService rejects null S3 service", () =>
                    Check.Throws<ArgumentNullException>(() => new CollectionService(null, QuietLogging()))),

                Case(s, "Ctor.NullLogging", "CollectionService rejects null logging", () =>
                    Check.Throws<ArgumentNullException>(() => new CollectionService(NewS3Service(out _), null))),

                Case(s, "Create.AndRetrieve", "Create a collection and read it back", async () =>
                {
                    CollectionService service = NewCollectionService();
                    Collection created = await service.CreateCollectionAsync("Widgets", "widgets");
                    Check.Equal("widgets", created.Slug);

                    List<Collection> all = await service.GetAllCollectionsAsync();
                    Check.True(all.Any(c => c.Slug == "widgets"), "GetAll should include the new collection");

                    Collection fetched = await service.GetCollectionBySlugAsync("WIDGETS");
                    Check.Equal("widgets", fetched.Slug);
                }),

                Case(s, "Create.ReservedSlug", "Creating the reserved 'config' slug is rejected", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<ArgumentException>(() => service.CreateCollectionAsync("Config", "config"));
                }),

                Case(s, "Create.Duplicate", "Creating a duplicate slug conflicts", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await service.CreateCollectionAsync("Widgets", "widgets");
                    await Check.ThrowsAsync<InvalidOperationException>(() => service.CreateCollectionAsync("Widgets Again", "widgets"));
                }),

                Case(s, "Create.NullArgs", "Create rejects null name and slug", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.CreateCollectionAsync(null, "slug"));
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.CreateCollectionAsync("name", null));
                }),

                Case(s, "Get.MissingSlug", "Reading a missing slug throws KeyNotFound", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.GetCollectionBySlugAsync("ghost"));
                }),

                Case(s, "Get.NullSlug", "Reading a null slug throws ArgumentNull", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.GetCollectionBySlugAsync(null));
                }),

                Case(s, "Delete.Missing", "Deleting a missing collection throws KeyNotFound", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.DeleteCollectionAsync("ghost"));
                }),

                Case(s, "Delete.RemovesCollectionAndFiles", "Deleting a collection removes it and its files", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await service.CreateCollectionAsync("Widgets", "widgets");
                    await service.UploadFileAsync("widgets", "a.txt", new MemoryStream(Encoding.UTF8.GetBytes("x")), "text/plain");

                    await service.DeleteCollectionAsync("widgets");

                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.GetCollectionBySlugAsync("widgets"));
                    List<Collection> all = await service.GetAllCollectionsAsync();
                    Check.False(all.Any(c => c.Slug == "widgets"), "Deleted collection should be gone");
                }),

                Case(s, "Upload.MissingCollection", "Uploading to a missing collection throws KeyNotFound", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<KeyNotFoundException>(() =>
                        service.UploadFileAsync("ghost", "a.txt", new MemoryStream(Encoding.UTF8.GetBytes("x")), "text/plain"));
                }),

                Case(s, "File.Lifecycle", "Upload, list, get metadata, download, then delete a file", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await service.CreateCollectionAsync("Widgets", "widgets");
                    await service.UploadFileAsync("widgets", "report.txt", new MemoryStream(Encoding.UTF8.GetBytes("payload")), "text/plain");

                    List<CollectionFile> files = await service.GetCollectionFilesAsync("widgets");
                    Check.True(files.Any(f => f.FileName == "report.txt"), "Listing should include the uploaded file");

                    CollectionFile metadata = await service.GetFileMetadataAsync("widgets", "report.txt");
                    Check.Equal(7L, metadata.Size);

                    DownloadFileResult download = await service.DownloadFileAsync("widgets", "report.txt");
                    using (StreamReader reader = new StreamReader(download.Content))
                        Check.Equal("payload", reader.ReadToEnd());

                    await service.DeleteFileAsync("widgets", "report.txt");
                    Check.Equal(0, (await service.GetCollectionFilesAsync("widgets")).Count);
                }),

                Case(s, "File.DeleteMissing", "Deleting a missing file throws KeyNotFound", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await service.CreateCollectionAsync("Widgets", "widgets");
                    await Check.ThrowsAsync<KeyNotFoundException>(() => service.DeleteFileAsync("widgets", "ghost.txt"));
                }),

                Case(s, "File.DeleteBatchEmpty", "Batch delete rejects an empty file list", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await service.CreateCollectionAsync("Widgets", "widgets");
                    await Check.ThrowsAsync<ArgumentException>(() => service.DeleteFilesAsync("widgets", new List<string>()));
                }),

                Case(s, "File.NullArgs", "File operations reject null slug and file name", async () =>
                {
                    CollectionService service = NewCollectionService();
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.GetCollectionFilesAsync(null));
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.GetFileMetadataAsync("widgets", null));
                    await Check.ThrowsAsync<ArgumentNullException>(() => service.DeleteFileAsync(null, "a.txt"));
                })
            };

            return new TestSuiteDescriptor(s, "Collection management service", cases);
        }

        private static CollectionService NewCollectionService()
        {
            return new CollectionService(NewS3Service(out _), QuietLogging());
        }

        private static S3Service NewS3Service(out FakeS3ClientAdapter client)
        {
            client = new FakeS3ClientAdapter();
            return new S3Service(CreateS3Settings(), QuietLogging(), client);
        }
    }
}
