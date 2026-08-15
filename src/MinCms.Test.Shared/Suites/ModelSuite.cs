namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>Domain model and DTO behavior.</summary>
        public static TestSuiteDescriptor ModelSuite()
        {
            const string s = "Model";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(s, "Collection.Defaults", "Collection default constructor populates sane defaults", () =>
                {
                    Collection c = new Collection();
                    Check.False(String.IsNullOrEmpty(c.Id), "Id should default to a non-empty GUID");
                    Check.Equal("My Collection", c.Name);
                    Check.Equal("my-collection", c.Slug);
                    Check.True(c.IsActive, "IsActive should default true");
                    Check.NotEqual(default(DateTime), c.CreatedUtc, "CreatedUtc should be initialized");
                }),

                Case(s, "Collection.Ctor.NameSlug", "Collection(name, slug) assigns name and slug", () =>
                {
                    Collection c = new Collection("Widgets", "widgets");
                    Check.Equal("Widgets", c.Name);
                    Check.Equal("widgets", c.Slug);
                }),

                Case(s, "Collection.Ctor.NullName", "Collection(name, slug) rejects null name", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection(null, "slug"))),

                Case(s, "Collection.Ctor.EmptyName", "Collection(name, slug) rejects empty name", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection("", "slug"))),

                Case(s, "Collection.Ctor.NullSlug", "Collection(name, slug) rejects null slug", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection("name", null))),

                Case(s, "Collection.Id.Empty", "Collection.Id setter rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection().Id = "")),

                Case(s, "Collection.Name.Empty", "Collection.Name setter rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection().Name = "")),

                Case(s, "Collection.Slug.Empty", "Collection.Slug setter rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new Collection().Slug = "")),

                Case(s, "CollectionFile.Defaults", "CollectionFile default constructor populates defaults", () =>
                {
                    CollectionFile f = new CollectionFile();
                    Check.Equal("", f.Key);
                    Check.Equal("", f.FileName);
                    Check.Equal(0L, f.Size);
                    Check.Equal(Constants.BinaryContentType, f.ContentType);
                    Check.Null(f.ETag);
                }),

                Case(s, "CollectionFile.ContentType.EmptyFallback", "CollectionFile.ContentType falls back to binary on empty", () =>
                {
                    CollectionFile f = new CollectionFile();
                    f.ContentType = "text/plain";
                    Check.Equal("text/plain", f.ContentType);
                    f.ContentType = "";
                    Check.Equal(Constants.BinaryContentType, f.ContentType);
                }),

                Case(s, "CollectionFile.Key.Empty", "CollectionFile.Key setter rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new CollectionFile().Key = "")),

                Case(s, "CollectionFile.FileName.Empty", "CollectionFile.FileName setter rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new CollectionFile().FileName = "")),

                Case(s, "CollectionFile.Mutators", "CollectionFile stores size, etag, timestamp", () =>
                {
                    DateTime when = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                    CollectionFile f = new CollectionFile
                    {
                        Key = "alpha/report.pdf",
                        FileName = "report.pdf",
                        Size = 4096,
                        ETag = "etag-1",
                        LastModifiedUtc = when
                    };
                    Check.Equal("alpha/report.pdf", f.Key);
                    Check.Equal(4096L, f.Size);
                    Check.Equal("etag-1", f.ETag);
                    Check.Equal(when, f.LastModifiedUtc);
                }),

                Case(s, "ApiError.StatusAndMessageMapping", "ApiErrorResponse maps every code to status and message", () =>
                {
                    Dictionary<ApiErrorEnum, int> expected = new Dictionary<ApiErrorEnum, int>
                    {
                        [ApiErrorEnum.AuthenticationFailed] = 401,
                        [ApiErrorEnum.BadRequest] = 400,
                        [ApiErrorEnum.Conflict] = 409,
                        [ApiErrorEnum.InternalError] = 500,
                        [ApiErrorEnum.NotFound] = 404,
                        [ApiErrorEnum.Timeout] = 408,
                        [ApiErrorEnum.TooLarge] = 413
                    };

                    foreach (KeyValuePair<ApiErrorEnum, int> kvp in expected)
                    {
                        ApiErrorResponse response = new ApiErrorResponse { Error = kvp.Key };
                        Check.Equal(kvp.Value, response.StatusCode, "StatusCode for " + kvp.Key);
                        Check.False(String.IsNullOrEmpty(response.Message), "Message for " + kvp.Key + " should be non-empty");
                    }
                }),

                Case(s, "ApiError.Defaults", "ApiErrorResponse defaults to AuthenticationFailed/401", () =>
                {
                    ApiErrorResponse response = new ApiErrorResponse();
                    Check.Equal(ApiErrorEnum.AuthenticationFailed, response.Error);
                    Check.Equal(401, response.StatusCode);
                }),

                Case(s, "ApiError.Ctor", "ApiErrorResponse(error, context, description) assigns members", () =>
                {
                    ApiErrorResponse response = new ApiErrorResponse(ApiErrorEnum.NotFound, new { key = "value" }, "missing");
                    Check.Equal(ApiErrorEnum.NotFound, response.Error);
                    Check.Equal(404, response.StatusCode);
                    Check.Equal("missing", response.Description);
                    Check.NotNull(response.Context);
                }),

                Case(s, "DeleteFilesRequest.Defaults", "DeleteFilesRequest defaults to an empty non-null list", () =>
                {
                    DeleteFilesRequest request = new DeleteFilesRequest();
                    Check.NotNull(request.FileNames);
                    Check.Equal(0, request.FileNames.Count);
                    request.FileNames.Add("a.txt");
                    Check.Equal(1, request.FileNames.Count);
                }),

                Case(s, "DeleteFilesResponse.Count", "DeleteFilesResponse stores a deleted count", () =>
                {
                    DeleteFilesResponse response = new DeleteFilesResponse { DeletedCount = 3 };
                    Check.Equal(3, response.DeletedCount);
                }),

                Case(s, "DownloadFileResult.Defaults", "DownloadFileResult defaults are null/zero", () =>
                {
                    DownloadFileResult result = new DownloadFileResult();
                    Check.Null(result.Content);
                    Check.Null(result.ContentType);
                    Check.Null(result.FileName);
                    Check.Equal(0L, result.ContentLength);
                }),

                Case(s, "Constants.Populated", "Constants expose product identity and content types", () =>
                {
                    Check.Equal("MinCMS", Constants.ProductName);
                    Check.False(String.IsNullOrEmpty(Constants.Version), "Version should be set");
                    Check.Equal("application/json", Constants.JsonContentType);
                    Check.Equal("application/octet-stream", Constants.BinaryContentType);
                    Check.Equal("config/collections.json", Constants.CollectionsConfigKey);
                })
            };

            return new TestSuiteDescriptor(s, "Domain models and DTOs", cases);
        }
    }
}
