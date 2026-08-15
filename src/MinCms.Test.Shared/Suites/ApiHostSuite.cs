namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Core.Settings;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        private static readonly JsonSerializerOptions _ApiJson = BuildApiJsonOptions();

        /// <summary>End-to-end HTTP behavior of the MinCMS API host.</summary>
        public static TestSuiteDescriptor ApiHostSuite()
        {
            const string s = "ApiHost";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                // ---------- Authentication ----------
                Case(s, "Auth.MissingKey", "Protected route without credentials returns 401", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/v1.0/collections");
                    Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                    ApiErrorResponse error = FromJson<ApiErrorResponse>(await response.Content.ReadAsStringAsync());
                    Check.Equal(ApiErrorEnum.AuthenticationFailed, error.Error);
                }),

                Case(s, "Auth.WrongKey", "Protected route with an unknown key returns 401", async () =>
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/v1.0/collections");
                    request.Headers.Add("x-api-key", "not-a-real-key");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                }),

                Case(s, "Auth.ApiKeyHeader", "Valid x-api-key authenticates", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                }),

                Case(s, "Auth.BearerToken", "Valid bearer token authenticates", async () =>
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/v1.0/collections");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiHost.ApiKey);
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                }),

                // ---------- Collections ----------
                Case(s, "Collections.List", "List collections returns seeded data", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    List<Collection> collections = FromJson<List<Collection>>(await response.Content.ReadAsStringAsync());
                    Check.True(collections.Any(c => c.Slug == "alpha"), "Listing should include the seeded alpha collection");
                }),

                Case(s, "Collections.GetBySlug", "Get a collection by slug returns 200", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/alpha");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    Collection collection = FromJson<Collection>(await response.Content.ReadAsStringAsync());
                    Check.Equal("alpha", collection.Slug);
                }),

                Case(s, "Collections.GetMissing", "Get a missing collection returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/ghost");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Collections.Create", "Create a collection returns 201", async () =>
                {
                    await QuietDelete("/v1.0/collections/created-suite");
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections");
                    request.Content = JsonBody("{\"Name\":\"Created\",\"Slug\":\"created-suite\"}");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.Created, response.StatusCode);
                    Collection created = FromJson<Collection>(await response.Content.ReadAsStringAsync());
                    Check.Equal("created-suite", created.Slug);
                    await QuietDelete("/v1.0/collections/created-suite");
                }),

                Case(s, "Collections.CreateDuplicate", "Creating a duplicate slug returns 409", async () =>
                {
                    await QuietDelete("/v1.0/collections/dup-suite");
                    using (HttpRequestMessage first = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections"))
                    {
                        first.Content = JsonBody("{\"Name\":\"Dup\",\"Slug\":\"dup-suite\"}");
                        using HttpResponseMessage firstResponse = await ApiHost.Client.SendAsync(first);
                        Check.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
                    }

                    using (HttpRequestMessage second = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections"))
                    {
                        second.Content = JsonBody("{\"Name\":\"Dup\",\"Slug\":\"dup-suite\"}");
                        using HttpResponseMessage secondResponse = await ApiHost.Client.SendAsync(second);
                        Check.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
                    }

                    await QuietDelete("/v1.0/collections/dup-suite");
                }),

                Case(s, "Collections.CreateMissingBody", "Create with no body returns 400", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections");
                    request.Content = JsonBody("");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }),

                Case(s, "Collections.Delete", "Delete a collection returns 204", async () =>
                {
                    await QuietDelete("/v1.0/collections/del-suite");
                    using (HttpRequestMessage create = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections"))
                    {
                        create.Content = JsonBody("{\"Name\":\"Del\",\"Slug\":\"del-suite\"}");
                        using HttpResponseMessage createResponse = await ApiHost.Client.SendAsync(create);
                        Check.Equal(HttpStatusCode.Created, createResponse.StatusCode);
                    }

                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/del-suite");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NoContent, response.StatusCode);
                }),

                Case(s, "Collections.DeleteMissing", "Delete a missing collection returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/ghost");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                // ---------- Files ----------
                Case(s, "Files.List", "List files returns seeded file", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/alpha/files");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    List<CollectionFile> files = FromJson<List<CollectionFile>>(await response.Content.ReadAsStringAsync());
                    Check.True(files.Any(f => f.FileName == "sample.txt"), "Listing should include the seeded sample.txt");
                }),

                Case(s, "Files.ListMissingCollection", "List files for a missing collection returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/ghost/files");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Files.Upload", "Upload a file returns 201 and metadata", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files");
                    request.Content = FileUpload(Encoding.UTF8.GetBytes("hello world"), "uploaded-suite.txt", "text/plain");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.Created, response.StatusCode);
                    CollectionFile uploaded = FromJson<CollectionFile>(await response.Content.ReadAsStringAsync());
                    Check.Equal("uploaded-suite.txt", uploaded.FileName);
                    await QuietDelete("/v1.0/collections/alpha/files/uploaded-suite.txt");
                }),

                Case(s, "Files.UploadMissingCollection", "Upload to a missing collection returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/ghost/files");
                    request.Content = FileUpload(Encoding.UTF8.GetBytes("data"), "x.txt", "text/plain");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Files.UploadLargeChunked", "Upload a large chunked file returns 201 with correct size", async () =>
                {
                    byte[] payload = CreatePayload((6 * 1024 * 1024) + 321);
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files");
                    MultipartFormDataContent content = new MultipartFormDataContent();
                    StreamContent fileContent = new StreamContent(new NonSeekableReadStream(payload));
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    content.Add(fileContent, "file", "large-suite.bin");
                    request.Content = content;

                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.Created, response.StatusCode);
                    CollectionFile uploaded = FromJson<CollectionFile>(await response.Content.ReadAsStringAsync());
                    Check.Equal("large-suite.bin", uploaded.FileName);
                    Check.Equal(payload.LongLength, uploaded.Size);
                    Check.Equal("application/octet-stream", uploaded.ContentType);
                    await QuietDelete("/v1.0/collections/alpha/files/large-suite.bin");
                }),

                Case(s, "Files.GetMetadata", "Get file metadata returns 200", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/alpha/files/sample.txt");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    CollectionFile metadata = FromJson<CollectionFile>(await response.Content.ReadAsStringAsync());
                    Check.Equal("sample.txt", metadata.FileName);
                }),

                Case(s, "Files.GetMetadataMissing", "Get metadata for a missing file returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/v1.0/collections/alpha/files/ghost.txt");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Files.DeleteSingle", "Delete a single file returns 204", async () =>
                {
                    using (HttpRequestMessage upload = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files"))
                    {
                        upload.Content = FileUpload(Encoding.UTF8.GetBytes("bye"), "delete-me-suite.txt", "text/plain");
                        using HttpResponseMessage uploadResponse = await ApiHost.Client.SendAsync(upload);
                        Check.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
                    }

                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/alpha/files/delete-me-suite.txt");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NoContent, response.StatusCode);
                }),

                Case(s, "Files.DeleteMissing", "Delete a missing file returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/alpha/files/ghost.txt");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Files.BatchDelete", "Batch delete returns the deleted count", async () =>
                {
                    foreach (string name in new[] { "batch-a-suite.txt", "batch-b-suite.txt" })
                    {
                        using HttpRequestMessage upload = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files");
                        upload.Content = FileUpload(Encoding.UTF8.GetBytes("data"), name, "text/plain");
                        using HttpResponseMessage uploadResponse = await ApiHost.Client.SendAsync(upload);
                        Check.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
                    }

                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/alpha/files");
                    request.Content = JsonBody("{\"FileNames\":[\"batch-a-suite.txt\",\"batch-b-suite.txt\"]}");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    DeleteFilesResponse deleted = FromJson<DeleteFilesResponse>(await response.Content.ReadAsStringAsync());
                    Check.Equal(2, deleted.DeletedCount);
                }),

                Case(s, "Files.BatchDeleteEmpty", "Batch delete with no file names returns 400", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, "/v1.0/collections/alpha/files");
                    request.Content = JsonBody("{\"FileNames\":[]}");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                }),

                // ---------- Documentation & health ----------
                Case(s, "Docs.OpenApi", "OpenAPI document lists routes and excludes downloads", async () =>
                {
                    string json = await ApiHost.Client.GetStringAsync("/openapi.json");
                    JsonObject paths = JsonNode.Parse(json)?["paths"]?.AsObject();
                    Check.NotNull(paths, "OpenAPI document should contain paths");
                    Check.True(paths.ContainsKey("/"), "should document root");
                    Check.True(paths.ContainsKey("/swagger"), "should document swagger");
                    Check.True(paths.ContainsKey("/openapi.json"), "should document openapi.json");
                    Check.True(paths.ContainsKey("/v1.0/collections"), "should document collections");
                    Check.True(paths.ContainsKey("/v1.0/collections/{slug}/files"), "should document files");
                    Check.False(paths.Any(kvp => kvp.Key.StartsWith("/download", StringComparison.OrdinalIgnoreCase)), "should not document download routes");

                    JsonObject createCollection = paths["/v1.0/collections"]?["post"]?.AsObject();
                    Check.NotNull(createCollection, "create collection should be documented");
                    Check.True(createCollection.ContainsKey("requestBody"), "create should document a request body");
                    Check.True(createCollection.ContainsKey("security"), "create should document security");
                }),

                Case(s, "Docs.Swagger", "Swagger UI is served", async () =>
                {
                    string html = await ApiHost.Client.GetStringAsync("/swagger");
                    Check.Contains("SwaggerUIBundle", html, StringComparison.Ordinal);
                }),

                Case(s, "Health.Root", "Root returns the operational landing page", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    string html = await response.Content.ReadAsStringAsync();
                    Check.Contains("MinCMS", html, StringComparison.Ordinal);
                }),

                Case(s, "Health.RootHead", "HEAD on root returns 200", async () =>
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Head, "/");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                }),

                Case(s, "Routing.Unknown", "Unknown route returns 404", async () =>
                {
                    using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Get, "/no/such/route");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                // ---------- CORS ----------
                Case(s, "Cors.Preflight", "Preflight returns CORS headers", async () =>
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/v1.0/collections");
                    request.Headers.Add("Origin", "http://localhost:8300");
                    request.Headers.Add("Access-Control-Request-Method", "POST");
                    request.Headers.Add("Access-Control-Request-Headers", "x-api-key,content-type");

                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.NoContent, response.StatusCode);
                    Check.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
                    string allowedMethods = response.Headers.GetValues("Access-Control-Allow-Methods").Single();
                    Check.Contains("POST", allowedMethods, StringComparison.OrdinalIgnoreCase);
                    string allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
                    Check.Contains("x-api-key", allowedHeaders, StringComparison.OrdinalIgnoreCase);
                }),

                Case(s, "Cors.PreflightDisallowedOrigin", "Preflight from a disallowed origin returns 403", async () =>
                {
                    using ApiHostContext ctx = ApiHost.CreateIsolated(settings =>
                        settings.Cors.AllowedOrigins = new List<string> { "http://allowed.example" });

                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/v1.0/collections");
                    request.Headers.Add("Origin", "http://evil.example");
                    request.Headers.Add("Access-Control-Request-Method", "POST");

                    using HttpResponseMessage response = await ctx.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                }),

                // ---------- Public download ----------
                Case(s, "Download.File", "Public download returns file content with attachment disposition", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/download/alpha/sample.txt");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    Check.Equal("seed file", await response.Content.ReadAsStringAsync());
                    string disposition = response.Content.Headers.ContentDisposition?.DispositionType ?? "";
                    Check.Contains("attachment", disposition, StringComparison.OrdinalIgnoreCase);
                }),

                Case(s, "Download.MissingFile", "Public download of a missing file returns 404", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/download/alpha/ghost.txt");
                    Check.Equal(HttpStatusCode.NotFound, response.StatusCode);
                }),

                Case(s, "Download.BrowseListing", "Public browse renders an HTML index", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/download/alpha");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    string html = await response.Content.ReadAsStringAsync();
                    Check.Contains("Index of /alpha/", html, StringComparison.Ordinal);
                    Check.Contains("sample.txt", html, StringComparison.Ordinal);
                }),

                Case(s, "Download.Sitemap", "Public sitemap renders XML", async () =>
                {
                    using HttpResponseMessage response = await ApiHost.Client.GetAsync("/download/alpha/sitemap.xml");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    string xml = await response.Content.ReadAsStringAsync();
                    Check.Contains("<urlset", xml, StringComparison.Ordinal);
                })
            };

            return new TestSuiteDescriptor(s, "API host HTTP endpoints", cases);
        }

        private static JsonSerializerOptions BuildApiJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static T FromJson<T>(string body)
        {
            return JsonSerializer.Deserialize<T>(body, _ApiJson);
        }

        private static StringContent JsonBody(string json)
        {
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static MultipartFormDataContent FileUpload(byte[] payload, string fileName, string contentType)
        {
            MultipartFormDataContent content = new MultipartFormDataContent();
            ByteArrayContent fileContent = new ByteArrayContent(payload);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(fileContent, "file", fileName);
            return content;
        }

        private static async Task QuietDelete(string path)
        {
            try
            {
                using HttpRequestMessage request = ApiHost.Authorized(HttpMethod.Delete, path);
                using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
