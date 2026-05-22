namespace MinCms.Server.Tests
{
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Core.Serialization;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class MinCmsApiHostTests : IClassFixture<MinCmsApiHostFixture>
    {
        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        static MinCmsApiHostTests()
        {
            _JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        private readonly MinCmsApiHostFixture _Fixture;

        public MinCmsApiHostTests(MinCmsApiHostFixture fixture)
        {
            _Fixture = fixture;
        }

        [Fact]
        public async Task Preflight_Returns_Cors_Headers()
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Options, "/v1.0/collections");
            request.Headers.Add("Origin", "http://localhost:8300");
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "x-api-key,content-type");

            using HttpResponseMessage response = await _Fixture.Client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());

            string allowedMethods = response.Headers.GetValues("Access-Control-Allow-Methods").Single();
            Assert.Contains("GET", allowedMethods, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("POST", allowedMethods, StringComparison.OrdinalIgnoreCase);

            string allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers").Single();
            Assert.Contains("x-api-key", allowedHeaders, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("content-type", allowedHeaders, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Protected_Route_Requires_Authentication()
        {
            using HttpResponseMessage response = await _Fixture.Client.GetAsync("/v1.0/collections");
            string body = await response.Content.ReadAsStringAsync();
            ApiErrorResponse? error = JsonSerializer.Deserialize<ApiErrorResponse>(body, _JsonOptions);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.NotNull(error);
            Assert.Equal(ApiErrorEnum.AuthenticationFailed, error.Error);
        }

        [Fact]
        public async Task Protected_Route_Returns_Collections_With_Authentication()
        {
            using HttpRequestMessage request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1.0/collections");
            using HttpResponseMessage response = await _Fixture.Client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();
            List<Collection>? collections = JsonSerializer.Deserialize<List<Collection>>(body, _JsonOptions);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(collections);
            Assert.Contains(collections, c => c.Slug == "alpha");
        }

        [Fact]
        public async Task OpenApi_Documents_NonDownload_Routes_And_Excludes_Downloads()
        {
            string json = await _Fixture.Client.GetStringAsync("/openapi.json");
            JsonNode? root = JsonNode.Parse(json);
            JsonObject? paths = root?["paths"]?.AsObject();

            Assert.NotNull(paths);
            Assert.True(paths.ContainsKey("/"));
            Assert.True(paths.ContainsKey("/swagger"));
            Assert.True(paths.ContainsKey("/openapi.json"));
            Assert.True(paths.ContainsKey("/v1.0/collections"));
            Assert.True(paths.ContainsKey("/v1.0/collections/{slug}/files"));
            Assert.DoesNotContain("/download/{slug}", paths.Select(kvp => kvp.Key));
            Assert.DoesNotContain(paths, kvp => kvp.Key.StartsWith("/download", StringComparison.OrdinalIgnoreCase));

            JsonObject? collectionsPath = paths["/v1.0/collections"]?.AsObject();
            JsonObject? createCollection = collectionsPath?["post"]?.AsObject();
            JsonObject? openApiPath = paths["/openapi.json"]?.AsObject();
            JsonObject? swaggerPath = paths["/swagger"]?.AsObject();
            Assert.NotNull(createCollection);
            Assert.NotNull(collectionsPath);
            Assert.NotNull(openApiPath);
            Assert.NotNull(swaggerPath);
            Assert.True(createCollection.ContainsKey("requestBody"));
            Assert.True(createCollection.ContainsKey("security"));
            Assert.True(collectionsPath.ContainsKey("options"));
            Assert.True(openApiPath.ContainsKey("options"));
            Assert.True(swaggerPath.ContainsKey("options"));
        }

        [Fact]
        public async Task SwaggerUi_Is_Served()
        {
            string html = await _Fixture.Client.GetStringAsync("/swagger");
            Assert.Contains("SwaggerUIBundle", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Multipart_Upload_And_Batch_Delete_Work()
        {
            using MultipartFormDataContent uploadContent = new MultipartFormDataContent();
            using ByteArrayContent fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("hello world"));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
            uploadContent.Add(fileContent, "file", "uploaded.txt");

            using HttpRequestMessage uploadRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/v1.0/collections/alpha/files");
            uploadRequest.Content = uploadContent;

            using HttpResponseMessage uploadResponse = await _Fixture.Client.SendAsync(uploadRequest);
            string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            CollectionFile? uploaded = JsonSerializer.Deserialize<CollectionFile>(uploadBody, _JsonOptions);

            Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
            Assert.NotNull(uploaded);
            Assert.Equal("uploaded.txt", uploaded.FileName);

            using HttpRequestMessage deleteRequest = CreateAuthenticatedRequest(HttpMethod.Delete, "/v1.0/collections/alpha/files");
            deleteRequest.Content = new StringContent("{\"FileNames\":[\"uploaded.txt\"]}", Encoding.UTF8, "application/json");

            using HttpResponseMessage deleteResponse = await _Fixture.Client.SendAsync(deleteRequest);
            string deleteBody = await deleteResponse.Content.ReadAsStringAsync();
            DeleteFilesResponse? deleted = JsonSerializer.Deserialize<DeleteFilesResponse>(deleteBody, _JsonOptions);

            Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
            Assert.NotNull(deleted);
            Assert.Equal(1, deleted.DeletedCount);
        }

        [Fact]
        public async Task Multipart_Upload_Accepts_Large_Chunked_File()
        {
            byte[] payload = CreatePayload((18 * 1024 * 1024) + 321);

            using MultipartFormDataContent uploadContent = new MultipartFormDataContent();
            using StreamContent fileContent = new StreamContent(new NonSeekableReadStream(payload));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.presentationml.presentation");
            uploadContent.Add(fileContent, "file", "presentation.pptx");

            using HttpRequestMessage uploadRequest = CreateAuthenticatedRequest(HttpMethod.Post, "/v1.0/collections/alpha/files");
            uploadRequest.Content = uploadContent;

            using HttpResponseMessage uploadResponse = await _Fixture.Client.SendAsync(uploadRequest);
            string uploadBody = await uploadResponse.Content.ReadAsStringAsync();
            CollectionFile? uploaded = JsonSerializer.Deserialize<CollectionFile>(uploadBody, _JsonOptions);

            Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
            Assert.NotNull(uploaded);
            Assert.Equal("presentation.pptx", uploaded.FileName);
            Assert.Equal(payload.LongLength, uploaded.Size);
            Assert.Equal("application/vnd.openxmlformats-officedocument.presentationml.presentation", uploaded.ContentType);
        }

        [Fact]
        public async Task Public_Download_Returns_File_Content()
        {
            using HttpResponseMessage response = await _Fixture.Client.GetAsync("/download/alpha/sample.txt");
            string body = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("seed file", body);
            Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType ?? response.Content.Headers.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ServerSettings_Deserialize_Null_Cors_Uses_Defaults()
        {
            Serializer serializer = new Serializer();
            ServerSettings settings = serializer.DeserializeJson<ServerSettings>("{\"Cors\":null}");

            Assert.NotNull(settings.Cors);
            Assert.Equal("*", Assert.Single(settings.Cors.AllowedOrigins));
            Assert.Contains("OPTIONS", settings.Cors.AllowedMethods);
            Assert.Equal("*", Assert.Single(settings.Cors.AllowedHeaders));
        }

        private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string path)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            request.Headers.Add("x-api-key", "test-key");
            return request;
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
    }

    public sealed class MinCmsApiHostFixture : IAsyncLifetime
    {
        private MinCmsApiHost? _Host;

        public HttpClient Client { get; private set; } = null!;

        public Task InitializeAsync()
        {
            int port = GetFreePort();
            ServerSettings settings = new ServerSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                AccessKeys = new List<AccessKeyEntry>
                {
                    new AccessKeyEntry("Test", "test-key")
                },
                Cors = new CorsSettings()
            };

            _Host = new MinCmsApiHost(settings, new LoggingModule(), new InMemoryCollectionService());
            Client = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port)
            };

            return _Host.StartAsync();
        }

        public Task DisposeAsync()
        {
            Client.Dispose();
            _Host?.Dispose();
            return Task.CompletedTask;
        }

        private static int GetFreePort()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    internal sealed class InMemoryCollectionService : ICollectionService
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<string, Collection> _Collections = new Dictionary<string, Collection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, FileRecord>> _Files = new Dictionary<string, Dictionary<string, FileRecord>>(StringComparer.OrdinalIgnoreCase);

        public InMemoryCollectionService()
        {
            Collection collection = new Collection("Alpha", "alpha")
            {
                Id = "alpha-id",
                CreatedUtc = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            _Collections[collection.Slug] = CloneCollection(collection);
            _Files[collection.Slug] = new Dictionary<string, FileRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["sample.txt"] = new FileRecord
                {
                    Content = Encoding.UTF8.GetBytes("seed file"),
                    ContentType = "text/plain",
                    LastModifiedUtc = DateTime.UtcNow.AddHours(-1),
                    ETag = "seed-etag"
                }
            };
        }

        public Task<List<Collection>> GetAllCollectionsAsync(CancellationToken token = default)
        {
            lock (_Sync)
            {
                return Task.FromResult(_Collections.Values.Select(CloneCollection).ToList());
            }
        }

        public Task<Collection> GetCollectionBySlugAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Collections.TryGetValue(slug, out Collection? collection))
                    throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");

                return Task.FromResult(CloneCollection(collection!));
            }
        }

        public Task<Collection> CreateCollectionAsync(string name, string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (_Collections.ContainsKey(slug))
                    throw new InvalidOperationException("A collection with slug '" + slug + "' already exists.");

                Collection collection = new Collection(name, slug)
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedUtc = DateTime.UtcNow,
                    IsActive = true
                };

                _Collections[slug] = CloneCollection(collection);
                _Files[slug] = new Dictionary<string, FileRecord>(StringComparer.OrdinalIgnoreCase);
                return Task.FromResult(CloneCollection(collection));
            }
        }

        public Task DeleteCollectionAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Collections.Remove(slug))
                    throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");

                _Files.Remove(slug);
                return Task.CompletedTask;
            }
        }

        public Task<List<CollectionFile>> GetCollectionFilesAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                return Task.FromResult(_Files[slug].Select(kvp => ToCollectionFile(slug, kvp.Key, kvp.Value)).ToList());
            }
        }

        public async Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default)
        {
            using MemoryStream ms = new MemoryStream();
            await content.CopyToAsync(ms, token).ConfigureAwait(false);

            lock (_Sync)
            {
                EnsureCollection(slug);
                _Files[slug][fileName] = new FileRecord
                {
                    Content = ms.ToArray(),
                    ContentType = String.IsNullOrEmpty(contentType) ? Constants.BinaryContentType : contentType,
                    LastModifiedUtc = DateTime.UtcNow,
                    ETag = Guid.NewGuid().ToString("N")
                };
            }
        }

        public Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].TryGetValue(fileName, out FileRecord? record))
                    throw new FileNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.FromResult(new DownloadFileResult
                {
                    Content = new MemoryStream(record!.Content, writable: false),
                    ContentLength = record.Content.LongLength,
                    ContentType = record.ContentType,
                    FileName = fileName
                });
            }
        }

        public Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].Remove(fileName))
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.CompletedTask;
            }
        }

        public Task<int> DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                foreach (string fileName in fileNames)
                {
                    _Files[slug].Remove(fileName);
                }

                return Task.FromResult(fileNames.Count);
            }
        }

        public Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].TryGetValue(fileName, out FileRecord? record))
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.FromResult(ToCollectionFile(slug, fileName, record!));
            }
        }

        private void EnsureCollection(string slug)
        {
            if (!_Collections.ContainsKey(slug))
                throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");
        }

        private static Collection CloneCollection(Collection input)
        {
            return new Collection
            {
                Id = input.Id,
                Name = input.Name,
                Slug = input.Slug,
                CreatedUtc = input.CreatedUtc,
                IsActive = input.IsActive
            };
        }

        private static CollectionFile ToCollectionFile(string slug, string fileName, FileRecord record)
        {
            return new CollectionFile
            {
                Key = slug + "/" + fileName,
                FileName = fileName,
                Size = record.Content.LongLength,
                LastModifiedUtc = record.LastModifiedUtc,
                ContentType = record.ContentType,
                ETag = record.ETag
            };
        }

        private sealed class FileRecord
        {
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public string ContentType { get; set; } = Constants.BinaryContentType;
            public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
            public string ETag { get; set; } = Guid.NewGuid().ToString("N");
        }
    }
}
