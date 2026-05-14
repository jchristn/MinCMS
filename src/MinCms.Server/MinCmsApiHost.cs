namespace MinCms.Server
{
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver;
    using WatsonWebserver.Core;
    using WatsonWebserver.Core.OpenApi;
    using WatsonWebserver.Core.Routing;
    using ApiErrorResponse = MinCms.Core.ApiErrorResponse;
    using Constants = MinCms.Core.Constants;
    using Serializer = MinCms.Core.Serialization.Serializer;

    /// <summary>
    /// Watson-backed MinCMS API host.
    /// </summary>
    public sealed class MinCmsApiHost : IDisposable
    {
        private const string _Header = "[MinCmsApiHost] ";
        private readonly ServerSettings _Settings;
        private readonly LoggingModule _Logging;
        private readonly ICollectionService _CollectionService;
        private readonly Serializer _Serializer = new Serializer();
        private readonly Webserver _Server;
        private readonly OpenApiSettings _OpenApiSettings;
        private readonly HashSet<string> _DocumentedPreflightPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Listener prefix.
        /// </summary>
        public string Prefix => _Server.Settings.Prefix;

        /// <summary>
        /// Underlying Watson server.
        /// </summary>
        public Webserver Server => _Server;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Server settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="collectionService">Collection service.</param>
        public MinCmsApiHost(ServerSettings settings, LoggingModule logging, ICollectionService collectionService)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
            _CollectionService = collectionService ?? throw new ArgumentNullException(nameof(collectionService));

            WebserverSettings webserverSettings = new WebserverSettings(_Settings.Rest.Hostname, _Settings.Rest.Port, _Settings.Rest.Ssl);
            _Server = new Webserver(webserverSettings, DefaultRouteAsync);
            _Server.Serializer = new MinCmsSerializer();
            _Server.Events.Logger = msg => _Logging.Debug(_Header + msg);

            _OpenApiSettings = BuildOpenApiSettings();

            ConfigureLifecycleRoutes();
            ConfigureDocumentationRoutes();
            ConfigureHealthRoutes();
            ConfigureApiRoutes();
            ConfigureDownloadRoutes();
        }

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken token = default)
        {
            _Server.Start(token);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            _Server.Dispose();
        }

        private void ConfigureLifecycleRoutes()
        {
            _Server.Routes.Preflight = HandlePreflightAsync;

            _Server.Routes.PreRouting = async (ctx) =>
            {
                ApplyCorsHeaders(ctx, includePreflightHeaders: false);
                ctx.Response.ContentType = Constants.JsonContentType;
                await Task.CompletedTask.ConfigureAwait(false);
            };

            _Server.Routes.PostRouting = async (ctx) =>
            {
                DateTime? start = ctx.Timestamp.Start;
                DateTime? end = ctx.Timestamp.End;
                DateTime effectiveEnd = end.HasValue && end.Value > DateTime.MinValue ? end.Value : DateTime.UtcNow;
                double elapsedMs = start.HasValue && start.Value > DateTime.MinValue ? (effectiveEnd - start.Value).TotalMilliseconds : 0;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + "(" + elapsedMs.ToString("F2") + "ms) "
                    + "from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);

                await Task.CompletedTask.ConfigureAwait(false);
            };

            _Server.Routes.Exception = HandleExceptionAsync;

            _Server.Routes.AuthenticateRequest = async (ctx) =>
            {
                string apiKey = ctx.Request.Headers.Get(Constants.ApiKeyHeader);

                if (String.IsNullOrEmpty(apiKey))
                {
                    string authHeader = ctx.Request.Headers.Get(Constants.AuthorizationHeader);
                    if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        apiKey = authHeader.Substring(7).Trim();
                    }
                }

                if (String.IsNullOrEmpty(apiKey))
                {
                    _Logging.Warn(_Header + "no auth material supplied for " + ctx.Request.Method + " from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.AuthenticationFailed, "Authentication required.").ConfigureAwait(false);
                    return;
                }

                AccessKeyEntry matchedKey = _Settings.AccessKeys.FirstOrDefault(k => String.Equals(k.Key, apiKey, StringComparison.Ordinal));
                if (matchedKey == null)
                {
                    _Logging.Warn(_Header + "API key from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " not found");
                    await SendApiErrorAsync(ctx, ApiErrorEnum.AuthenticationFailed, "Authentication required.").ConfigureAwait(false);
                    return;
                }

                ctx.Metadata = matchedKey.Name;
                _Logging.Debug(_Header + "authenticated key '" + matchedKey.Name + "' from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);
            };
        }

        private void ConfigureDocumentationRoutes()
        {
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                _OpenApiSettings.DocumentPath,
                ServeOpenApiDocumentAsync,
                openApiMetadata: CreatePublicRouteMetadata(
                    "GetOpenApiDocument",
                    "Retrieve the generated OpenAPI document",
                    "Documentation",
                    "Returns the generated OpenAPI 3.x document for all non-download routes.")
                    .WithResponse(200, JsonObjectResponse("Generated OpenAPI document"))
                    .WithResponse(500, ErrorResponse("OpenAPI generation failed")));

            RegisterDocumentedPreflightRoute(_OpenApiSettings.DocumentPath, "Documentation");

            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                _OpenApiSettings.SwaggerUiPath,
                SwaggerUiHandler.Create(_OpenApiSettings.DocumentPath, _OpenApiSettings.Info.Title),
                openApiMetadata: CreatePublicRouteMetadata(
                    "GetSwaggerUi",
                    "Open the Swagger UI",
                    "Documentation",
                    "Returns the interactive Swagger UI for the generated OpenAPI document.")
                    .WithResponse(200, HtmlResponse("Swagger UI page")));

            RegisterDocumentedPreflightRoute(_OpenApiSettings.SwaggerUiPath, "Documentation");
        }

        private void ConfigureHealthRoutes()
        {
            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.HEAD,
                "/",
                async (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                },
                openApiMetadata: CreatePublicRouteMetadata(
                    "HeadRoot",
                    "Health probe",
                    "Health",
                    "Lightweight readiness probe used for health checks.")
                    .WithResponse(200, OpenApiResponseMetadata.Create("Server is reachable.")));

            _Server.Routes.PreAuthentication.Static.Add(
                HttpMethod.GET,
                "/",
                async (ctx) =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = Constants.HtmlContentType;
                    await ctx.Response.Send(Constants.HtmlHomepage, ctx.Token).ConfigureAwait(false);
                },
                openApiMetadata: CreatePublicRouteMetadata(
                    "GetRoot",
                    "Get the API root page",
                    "Health",
                    "Returns the HTML landing page confirming that the MinCMS node is operational.")
                    .WithResponse(200, HtmlResponse("HTML landing page")));

            RegisterDocumentedPreflightRoute("/", "Health");
        }

        private void ConfigureApiRoutes()
        {
            MapApiRoute(
                HttpMethod.GET,
                "/v1.0/collections",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);

                    _Logging.Info(_Header + "list collections | key=" + keyName + " | source=" + sourceIp);

                    List<Collection> collections = await _CollectionService.GetAllCollectionsAsync(req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "list collections | key=" + keyName + " | source=" + sourceIp + " | result=Success | count=" + collections.Count);
                    return collections;
                },
                BuildListCollectionsMetadata(),
                auth: true);

            MapApiRoute<Collection>(
                HttpMethod.POST,
                "/v1.0/collections",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    Collection input = req.GetData<Collection>() ?? throw new ArgumentNullException(nameof(req), "Collection payload is required.");

                    _Logging.Info(_Header + "create collection | key=" + keyName + " | source=" + sourceIp + " | name=" + input.Name + " | slug=" + input.Slug);

                    Collection created = await _CollectionService.CreateCollectionAsync(input.Name, input.Slug, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "create collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + created.Slug + " | result=Success");

                    req.Http.Response.StatusCode = 201;
                    return created;
                },
                BuildCreateCollectionMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.GET,
                "/v1.0/collections/{slug}",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];

                    _Logging.Info(_Header + "get collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                    Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "get collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success");
                    return collection;
                },
                BuildGetCollectionMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.DELETE,
                "/v1.0/collections/{slug}",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];

                    _Logging.Info(_Header + "delete collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                    await _CollectionService.DeleteCollectionAsync(slug, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "delete collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success");

                    req.Http.Response.StatusCode = 204;
                    return null;
                },
                BuildDeleteCollectionMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.GET,
                "/v1.0/collections/{slug}/files",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];

                    _Logging.Info(_Header + "list files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                    List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "list files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success | count=" + files.Count);
                    return files;
                },
                BuildListFilesMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.POST,
                "/v1.0/collections/{slug}/files",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];

                    string contentType = req.Http.Request.ContentType;
                    string boundary = GetMultipartBoundary(contentType);
                    if (String.IsNullOrEmpty(boundary))
                        throw new ArgumentException("Request must be multipart/form-data.");

                    ParsedMultipartFile parsedFile = ParseMultipartFormData(req.Http.Request.DataAsBytes, boundary);
                    if (parsedFile == null || String.IsNullOrEmpty(parsedFile.FileName) || parsedFile.FileStream == null)
                        throw new ArgumentException("No file found in multipart form data.");

                    string decodedFileName = Uri.UnescapeDataString(parsedFile.FileName);

                    _Logging.Debug(_Header + "upload file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + decodedFileName);

                    await _CollectionService.UploadFileAsync(slug, decodedFileName, parsedFile.FileStream, parsedFile.ContentType, req.CancellationToken).ConfigureAwait(false);
                    CollectionFile metadata = await _CollectionService.GetFileMetadataAsync(slug, decodedFileName, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "upload file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + decodedFileName + " | result=Success");

                    req.Http.Response.StatusCode = 201;
                    return metadata;
                },
                BuildUploadFileMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.GET,
                "/v1.0/collections/{slug}/files/{fileName}",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];
                    string fileName = Uri.UnescapeDataString(req.Parameters["fileName"]);

                    _Logging.Info(_Header + "get file metadata | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                    CollectionFile metadata = await _CollectionService.GetFileMetadataAsync(slug, fileName, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "get file metadata | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success");
                    return metadata;
                },
                BuildGetFileMetadataMetadata(),
                auth: true);

            MapApiRoute<DeleteFilesRequest>(
                HttpMethod.DELETE,
                "/v1.0/collections/{slug}/files",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];
                    DeleteFilesRequest deleteRequest = req.GetData<DeleteFilesRequest>();

                    if (deleteRequest == null || deleteRequest.FileNames == null || deleteRequest.FileNames.Count < 1)
                        throw new ArgumentException("At least one filename is required in FileNames.");

                    _Logging.Info(_Header + "batch delete files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | count=" + deleteRequest.FileNames.Count);

                    int deletedCount = await _CollectionService.DeleteFilesAsync(slug, deleteRequest.FileNames, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "batch delete files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success | deleted=" + deletedCount);

                    return new DeleteFilesResponse { DeletedCount = deletedCount };
                },
                BuildDeleteFilesMetadata(),
                auth: true);

            MapApiRoute(
                HttpMethod.DELETE,
                "/v1.0/collections/{slug}/files/{fileName}",
                async (req) =>
                {
                    string keyName = GetKeyName(req);
                    string sourceIp = GetSourceIp(req);
                    string slug = req.Parameters["slug"];
                    string fileName = Uri.UnescapeDataString(req.Parameters["fileName"]);

                    _Logging.Info(_Header + "delete file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                    await _CollectionService.DeleteFileAsync(slug, fileName, req.CancellationToken).ConfigureAwait(false);

                    _Logging.Info(_Header + "delete file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success");

                    req.Http.Response.StatusCode = 204;
                    return null;
                },
                BuildDeleteFileMetadata(),
                auth: true);
        }

        private void ConfigureDownloadRoutes()
        {
            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/download/{slug}",
                async (ctx) =>
                {
                    string sourceIp = GetSourceIp(ctx);
                    string slug = ctx.Request.Url.Parameters["slug"];

                    _Logging.Info(_Header + "browse collection | key=anonymous | source=" + sourceIp + " | slug=" + slug);

                    Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, ctx.Token).ConfigureAwait(false);
                    if (!collection.IsActive)
                    {
                        await SendApiErrorAsync(ctx, ApiErrorEnum.NotFound, "Collection is not active.").ConfigureAwait(false);
                        return;
                    }

                    List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, ctx.Token).ConfigureAwait(false);

                    _Logging.Info(_Header + "browse collection | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | result=Success | count=" + files.Count);

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = Constants.HtmlContentType;
                    await ctx.Response.Send(BuildDirectoryListing(collection, files), ctx.Token).ConfigureAwait(false);
                });

            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/download/{slug}/sitemap.xml",
                async (ctx) =>
                {
                    string sourceIp = GetSourceIp(ctx);
                    string slug = ctx.Request.Url.Parameters["slug"];

                    _Logging.Info(_Header + "sitemap | key=anonymous | source=" + sourceIp + " | slug=" + slug);

                    Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, ctx.Token).ConfigureAwait(false);
                    List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, ctx.Token).ConfigureAwait(false);

                    _Logging.Info(_Header + "sitemap | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | result=Success");

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = Constants.XmlContentType;
                    await ctx.Response.Send(BuildSitemapXml(collection.Slug, files), ctx.Token).ConfigureAwait(false);
                });

            _Server.Routes.PreAuthentication.Parameter.Add(
                HttpMethod.GET,
                "/download/{slug}/{fileName}",
                async (ctx) =>
                {
                    string sourceIp = GetSourceIp(ctx);
                    string slug = ctx.Request.Url.Parameters["slug"];
                    string fileName = Uri.UnescapeDataString(ctx.Request.Url.Parameters["fileName"]);

                    _Logging.Info(_Header + "download | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                    DownloadFileResult downloadResult = await _CollectionService.DownloadFileAsync(slug, fileName, ctx.Token).ConfigureAwait(false);

                    _Logging.Info(_Header + "download | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success | size=" + downloadResult.ContentLength);

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = downloadResult.ContentType;
                    ctx.Response.ContentLength = downloadResult.ContentLength;

                    string downloadName =
                        downloadResult.FileName.Contains("/")
                        ? downloadResult.FileName.Substring(downloadResult.FileName.LastIndexOf('/') + 1)
                        : downloadResult.FileName;

                    SetHeader(ctx.Response.Headers, "Content-Disposition", "attachment; filename=\"" + downloadName + "\"");

                    byte[] fileBytes;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        await downloadResult.Content.CopyToAsync(ms, ctx.Token).ConfigureAwait(false);
                        fileBytes = ms.ToArray();
                    }

                    downloadResult.Content.Dispose();
                    await ctx.Response.Send(fileBytes, ctx.Token).ConfigureAwait(false);
                });
        }

        private async Task DefaultRouteAsync(HttpContextBase ctx)
        {
            await SendApiErrorAsync(ctx, ApiErrorEnum.NotFound, "No route matched the requested resource.").ConfigureAwait(false);
        }

        private async Task ServeOpenApiDocumentAsync(HttpContextBase ctx)
        {
            OpenApiDocumentGenerator generator = new OpenApiDocumentGenerator();
            string json = generator.Generate(_Server.Routes, _OpenApiSettings);

            JsonNode root = JsonNode.Parse(json);
            JsonObject paths = root?["paths"] as JsonObject;

            if (paths != null)
            {
                List<string> keysToRemove =
                    paths
                    .Select(kvp => kvp.Key)
                    .Where(key => key.StartsWith("/download", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (string key in keysToRemove)
                {
                    paths.Remove(key);
                }

                json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            SetHeader(ctx.Response.Headers, "Cache-Control", "no-cache, no-store, must-revalidate");
            SetHeader(ctx.Response.Headers, "Pragma", "no-cache");
            SetHeader(ctx.Response.Headers, "Expires", "0");
            await ctx.Response.Send(json, ctx.Token).ConfigureAwait(false);
        }

        private async Task HandlePreflightAsync(HttpContextBase ctx)
        {
            string origin = ctx.Request.Headers.Get("Origin");
            if (!TryResolveAllowedOrigin(origin, out string allowedOrigin))
            {
                ctx.Response.StatusCode = 403;
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            ApplyCorsHeaders(ctx, includePreflightHeaders: true, allowedOriginOverride: allowedOrigin);
            ctx.Response.StatusCode = 204;
            await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
        }

        private async Task HandleExceptionAsync(HttpContextBase ctx, Exception e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));

            switch (e)
            {
                case ArgumentOutOfRangeException:
                case ArgumentNullException:
                case ArgumentException:
                case FormatException:
                case JsonException:
                    _Logging.Warn(_Header + "bad request exception:" + Environment.NewLine + e);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.BadRequest, e.Message).ConfigureAwait(false);
                    return;
                case FileNotFoundException:
                case KeyNotFoundException:
                    _Logging.Warn(_Header + "not found exception:" + Environment.NewLine + e);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.NotFound, e.Message).ConfigureAwait(false);
                    return;
                case InvalidOperationException:
                    _Logging.Warn(_Header + "conflict exception:" + Environment.NewLine + e);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.Conflict, e.Message).ConfigureAwait(false);
                    return;
                case TaskCanceledException:
                case OperationCanceledException:
                    _Logging.Warn(_Header + "timeout exception:" + Environment.NewLine + e);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.Timeout, e.Message).ConfigureAwait(false);
                    return;
                default:
                    _Logging.Warn(_Header + "unhandled exception:" + Environment.NewLine + e);
                    await SendApiErrorAsync(ctx, ApiErrorEnum.InternalError, e.Message).ConfigureAwait(false);
                    return;
            }
        }

        private void MapApiRoute(
            HttpMethod method,
            string path,
            Func<ApiRequest, Task<object>> handler,
            OpenApiRouteMetadata openApiMetadata,
            bool auth)
        {
            RegisterApiRoute(method, path, WrapApiHandler(handler), openApiMetadata, auth);
        }

        private void MapApiRoute<T>(
            HttpMethod method,
            string path,
            Func<ApiRequest, Task<object>> handler,
            OpenApiRouteMetadata openApiMetadata,
            bool auth) where T : class
        {
            RegisterApiRoute(method, path, WrapApiHandler<T>(handler), openApiMetadata, auth);
        }

        private void RegisterApiRoute(
            HttpMethod method,
            string path,
            Func<HttpContextBase, Task> handler,
            OpenApiRouteMetadata openApiMetadata,
            bool auth)
        {
            RoutingGroup group = auth ? _Server.Routes.PostAuthentication : _Server.Routes.PreAuthentication;

            if (path.Contains('{'))
            {
                group.Parameter.Add(method, path, handler, openApiMetadata: openApiMetadata);
            }
            else
            {
                group.Static.Add(method, path, handler, openApiMetadata: openApiMetadata);
            }

            RegisterDocumentedPreflightRoute(path, path.Contains("/files/", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/files", StringComparison.OrdinalIgnoreCase) ? "Files" : "Collections");
        }

        private void RegisterDocumentedPreflightRoute(string path, string tag)
        {
            if (!_DocumentedPreflightPaths.Add(path))
                return;

            OpenApiRouteMetadata metadata = CreatePublicRouteMetadata(
                BuildPreflightOperationId(path),
                "CORS preflight",
                tag,
                "Returns the configured CORS policy for browser preflight requests targeting this endpoint.")
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(403, OpenApiResponseMetadata.Create("Origin is not permitted by the configured CORS policy."));

            if (path.Contains('{'))
            {
                foreach (string parameterName in GetPathParameterNames(path))
                {
                    metadata.WithParameter(OpenApiParameterMetadata.Path(parameterName, "Route parameter."));
                }

                _Server.Routes.PreAuthentication.Parameter.Add(HttpMethod.OPTIONS, path, HandlePreflightAsync, openApiMetadata: metadata);
            }
            else
            {
                _Server.Routes.PreAuthentication.Static.Add(HttpMethod.OPTIONS, path, HandlePreflightAsync, openApiMetadata: metadata);
            }
        }

        private Func<HttpContextBase, Task> WrapApiHandler(Func<ApiRequest, Task<object>> handler)
        {
            return async (ctx) =>
            {
                try
                {
                    ApiRequest request = new ApiRequest(ctx, _Server.Serializer, null, ctx.Token)
                    {
                        Metadata = ctx.Metadata
                    };

                    object result = await handler(request).ConfigureAwait(false);
                    await SendApiResultAsync(ctx, result).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await HandleExceptionAsync(ctx, e).ConfigureAwait(false);
                }
            };
        }

        private Func<HttpContextBase, Task> WrapApiHandler<T>(Func<ApiRequest, Task<object>> handler) where T : class
        {
            return async (ctx) =>
            {
                try
                {
                    T requestBody = null;

                    if (!String.IsNullOrEmpty(ctx.Request.DataAsString))
                    {
                        requestBody = _Server.Serializer.DeserializeJson<T>(ctx.Request.DataAsString);
                    }

                    ApiRequest request = new ApiRequest(ctx, _Server.Serializer, requestBody, ctx.Token)
                    {
                        Metadata = ctx.Metadata
                    };

                    object result = await handler(request).ConfigureAwait(false);
                    await SendApiResultAsync(ctx, result).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    await HandleExceptionAsync(ctx, e).ConfigureAwait(false);
                }
            };
        }

        private async Task SendApiResultAsync(HttpContextBase ctx, object result)
        {
            if (ctx.Response.ServerSentEvents || ctx.Response.ChunkedTransfer)
                return;

            if (result == null)
            {
                await ctx.Response.Send(ctx.Token).ConfigureAwait(false);
                return;
            }

            if (result is string stringResult)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";

                await ctx.Response.Send(stringResult, ctx.Token).ConfigureAwait(false);
                return;
            }

            Type resultType = result.GetType();
            if (resultType.IsPrimitive)
            {
                if (String.IsNullOrEmpty(ctx.Response.ContentType))
                    ctx.Response.ContentType = "text/plain";

                await ctx.Response.Send(result.ToString(), ctx.Token).ConfigureAwait(false);
                return;
            }

            if (String.IsNullOrEmpty(ctx.Response.ContentType))
                ctx.Response.ContentType = Constants.JsonContentType;

            await ctx.Response.Send(_Serializer.SerializeJson(result, true), ctx.Token).ConfigureAwait(false);
        }

        private async Task SendApiErrorAsync(HttpContextBase ctx, ApiErrorEnum error, string description)
        {
            ApiErrorResponse response = new ApiErrorResponse(error, null, description);
            ctx.Response.StatusCode = response.StatusCode;
            ctx.Response.ContentType = Constants.JsonContentType;
            await ctx.Response.Send(_Serializer.SerializeJson(response, true), ctx.Token).ConfigureAwait(false);
        }

        private void ApplyCorsHeaders(HttpContextBase ctx, bool includePreflightHeaders, string allowedOriginOverride = null)
        {
            string origin = ctx.Request.Headers.Get("Origin");
            string allowedOrigin = allowedOriginOverride;

            if (allowedOrigin == null && !TryResolveAllowedOrigin(origin, out allowedOrigin))
                return;

            if (!String.IsNullOrEmpty(allowedOrigin))
            {
                SetHeader(ctx.Response.Headers, "Access-Control-Allow-Origin", allowedOrigin);

                if (!String.Equals(allowedOrigin, "*", StringComparison.Ordinal))
                    AppendVaryHeader(ctx.Response.Headers, "Origin");
            }

            if (_Settings.Cors.ExposeHeaders != null && _Settings.Cors.ExposeHeaders.Count > 0)
            {
                SetHeader(ctx.Response.Headers, "Access-Control-Expose-Headers", String.Join(", ", _Settings.Cors.ExposeHeaders));
            }

            if (includePreflightHeaders)
            {
                if (_Settings.Cors.AllowedMethods != null && _Settings.Cors.AllowedMethods.Count > 0)
                {
                    SetHeader(ctx.Response.Headers, "Access-Control-Allow-Methods", String.Join(", ", _Settings.Cors.AllowedMethods));
                    SetHeader(ctx.Response.Headers, "Allow", String.Join(", ", _Settings.Cors.AllowedMethods));
                }

                SetHeader(ctx.Response.Headers, "Access-Control-Allow-Headers", BuildAllowedHeaders(ctx));
                SetHeader(ctx.Response.Headers, "Access-Control-Max-Age", _Settings.Cors.MaxAgeSeconds.ToString());
            }
        }

        private bool TryResolveAllowedOrigin(string origin, out string allowedOrigin)
        {
            allowedOrigin = null;

            List<string> allowedOrigins = _Settings.Cors?.AllowedOrigins ?? new List<string> { "*" };
            if (allowedOrigins.Any(o => String.Equals(o, "*", StringComparison.Ordinal)))
            {
                allowedOrigin = "*";
                return true;
            }

            if (String.IsNullOrEmpty(origin))
                return false;

            string matchedOrigin = allowedOrigins.FirstOrDefault(o => String.Equals(o, origin, StringComparison.OrdinalIgnoreCase));
            if (matchedOrigin == null)
                return false;

            allowedOrigin = origin;
            return true;
        }

        private string BuildAllowedHeaders(HttpContextBase ctx)
        {
            List<string> configuredHeaders = _Settings.Cors?.AllowedHeaders ?? new List<string> { "*" };

            if (configuredHeaders.Any(h => String.Equals(h, "*", StringComparison.Ordinal)))
            {
                string requestedHeaders = ctx.Request.Headers.Get("Access-Control-Request-Headers");
                if (!String.IsNullOrWhiteSpace(requestedHeaders))
                    return requestedHeaders;

                return Constants.ApiKeyHeader + ", " + Constants.AuthorizationHeader + ", Content-Type, Accept, Origin";
            }

            return String.Join(", ", configuredHeaders);
        }

        private static void SetHeader(System.Collections.Specialized.NameValueCollection headers, string name, string value)
        {
            headers.Remove(name);
            headers.Add(name, value);
        }

        private static void AppendVaryHeader(System.Collections.Specialized.NameValueCollection headers, string value)
        {
            string existing = headers.Get("Vary");
            if (String.IsNullOrWhiteSpace(existing))
            {
                SetHeader(headers, "Vary", value);
                return;
            }

            List<string> parts =
                existing
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !String.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!parts.Any(p => String.Equals(p, value, StringComparison.OrdinalIgnoreCase)))
                parts.Add(value);

            SetHeader(headers, "Vary", String.Join(", ", parts));
        }

        private OpenApiSettings BuildOpenApiSettings()
        {
            OpenApiSettings settings = new OpenApiSettings
            {
                DocumentPath = "/openapi.json",
                SwaggerUiPath = "/swagger"
            };

            settings.Info.Title = "MinCMS API";
            settings.Info.Version = Constants.Version.TrimStart('v', 'V');
            settings.Info.Description = "MinCMS is a minimal S3-backed content management API with key-based authentication and browser-compatible CORS support.";

            settings.Tags.Add(new OpenApiTag { Name = "Documentation", Description = "OpenAPI and Swagger discovery endpoints" });
            settings.Tags.Add(new OpenApiTag { Name = "Health", Description = "Operational health and landing page endpoints" });
            settings.Tags.Add(new OpenApiTag { Name = "Collections", Description = "Collection management endpoints" });
            settings.Tags.Add(new OpenApiTag { Name = "Files", Description = "File management endpoints within collections" });

            settings.SecuritySchemes["ApiKey"] = new OpenApiSecurityScheme
            {
                Type = "apiKey",
                Name = Constants.ApiKeyHeader,
                In = "header",
                Description = "API key supplied in the x-api-key header."
            };

            settings.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = "http",
                Scheme = "bearer",
                BearerFormat = "API key",
                Description = "API key supplied as a bearer token in the Authorization header."
            };

            RegisterSchemas(settings);
            return settings;
        }

        private void RegisterSchemas(OpenApiSettings settings)
        {
            settings.Schemas["Collection"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "Id", "Name", "Slug", "CreatedUtc", "IsActive" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Id"] = OpenApiSchemaMetadata.String(),
                    ["Name"] = OpenApiSchemaMetadata.String(),
                    ["Slug"] = OpenApiSchemaMetadata.String(),
                    ["CreatedUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["IsActive"] = OpenApiSchemaMetadata.Boolean()
                }
            };

            settings.Schemas["CreateCollectionRequest"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "Name", "Slug" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Name"] = OpenApiSchemaMetadata.String(),
                    ["Slug"] = OpenApiSchemaMetadata.String()
                }
            };

            settings.Schemas["CollectionFile"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "Key", "FileName", "Size", "LastModifiedUtc", "ContentType" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Key"] = OpenApiSchemaMetadata.String(),
                    ["FileName"] = OpenApiSchemaMetadata.String(),
                    ["Size"] = OpenApiSchemaMetadata.Integer("int64"),
                    ["LastModifiedUtc"] = OpenApiSchemaMetadata.String("date-time"),
                    ["ContentType"] = OpenApiSchemaMetadata.String(),
                    ["ETag"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true }
                }
            };

            settings.Schemas["FileUploadRequest"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "file" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["file"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Format = "binary",
                        Description = "File payload to upload."
                    }
                }
            };

            settings.Schemas["DeleteFilesRequest"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "FileNames" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["FileNames"] = OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.String())
                }
            };

            settings.Schemas["DeleteFilesResponse"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "DeletedCount" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["DeletedCount"] = OpenApiSchemaMetadata.Integer()
                }
            };

            settings.Schemas["ApiErrorResponse"] = new OpenApiSchemaMetadata
            {
                Type = "object",
                Required = new List<string> { "Error", "Message", "StatusCode" },
                Properties = new Dictionary<string, OpenApiSchemaMetadata>
                {
                    ["Error"] = new OpenApiSchemaMetadata
                    {
                        Type = "string",
                        Enum = Enum.GetNames<ApiErrorEnum>().Cast<object>().ToList()
                    },
                    ["Message"] = OpenApiSchemaMetadata.String(),
                    ["StatusCode"] = OpenApiSchemaMetadata.Integer(),
                    ["Context"] = new OpenApiSchemaMetadata { Type = "object", Nullable = true },
                    ["Description"] = new OpenApiSchemaMetadata { Type = "string", Nullable = true }
                }
            };
        }

        private OpenApiRouteMetadata BuildListCollectionsMetadata()
        {
            return CreateProtectedRouteMetadata(
                "ListCollections",
                "List all collections",
                "Collections",
                "Returns every configured collection.")
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("Collection"))))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildCreateCollectionMetadata()
        {
            return CreateProtectedRouteMetadata(
                "CreateCollection",
                "Create a new collection",
                "Collections",
                "Creates a new collection using the supplied name and slug.")
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.CreateRef("CreateCollectionRequest"), "Collection create payload"))
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaMetadata.CreateRef("Collection")))
                .WithResponse(400, ErrorResponse("Request body was invalid"))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(409, ErrorResponse("Collection slug already exists"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildGetCollectionMetadata()
        {
            return CreateProtectedRouteMetadata(
                "GetCollection",
                "Get a collection by slug",
                "Collections",
                "Returns a single collection identified by its slug.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateRef("Collection")))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildDeleteCollectionMetadata()
        {
            return CreateProtectedRouteMetadata(
                "DeleteCollection",
                "Delete a collection",
                "Collections",
                "Deletes the collection and all files stored beneath its prefix.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildListFilesMetadata()
        {
            return CreateProtectedRouteMetadata(
                "ListCollectionFiles",
                "List files in a collection",
                "Files",
                "Returns metadata for every file stored within the target collection.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateArray(OpenApiSchemaMetadata.CreateRef("CollectionFile"))))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildUploadFileMetadata()
        {
            return CreateProtectedRouteMetadata(
                "UploadFile",
                "Upload a file to a collection",
                "Files",
                "Uploads a single file using multipart/form-data and returns the stored file metadata.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Multipart(OpenApiSchemaMetadata.CreateRef("FileUploadRequest"), "Multipart file upload payload"))
                .WithResponse(201, OpenApiResponseMetadata.Created(OpenApiSchemaMetadata.CreateRef("CollectionFile")))
                .WithResponse(400, ErrorResponse("Multipart payload was invalid"))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildGetFileMetadataMetadata()
        {
            return CreateProtectedRouteMetadata(
                "GetFileMetadata",
                "Get metadata for a file",
                "Files",
                "Returns metadata for a single file in the target collection.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithParameter(OpenApiParameterMetadata.Path("fileName", "URL-encoded file name, including any virtual path segments"))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateRef("CollectionFile")))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection or file not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildDeleteFilesMetadata()
        {
            return CreateProtectedRouteMetadata(
                "DeleteFiles",
                "Delete multiple files from a collection",
                "Files",
                "Deletes one or more files identified by FileNames and returns the number of requested deletions.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithRequestBody(OpenApiRequestBodyMetadata.Json(OpenApiSchemaMetadata.CreateRef("DeleteFilesRequest"), "Batch delete request payload"))
                .WithResponse(200, OpenApiResponseMetadata.Ok(OpenApiSchemaMetadata.CreateRef("DeleteFilesResponse")))
                .WithResponse(400, ErrorResponse("At least one file name is required"))
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private OpenApiRouteMetadata BuildDeleteFileMetadata()
        {
            return CreateProtectedRouteMetadata(
                "DeleteFile",
                "Delete a file",
                "Files",
                "Deletes a single file from the target collection.")
                .WithParameter(OpenApiParameterMetadata.Path("slug", "Collection slug"))
                .WithParameter(OpenApiParameterMetadata.Path("fileName", "URL-encoded file name, including any virtual path segments"))
                .WithResponse(204, OpenApiResponseMetadata.NoContent())
                .WithResponse(401, ErrorResponse("Authentication required"))
                .WithResponse(404, ErrorResponse("Collection or file not found"))
                .WithResponse(408, ErrorResponse("Request timed out"))
                .WithResponse(500, ErrorResponse("Internal server error"));
        }

        private static OpenApiRouteMetadata CreatePublicRouteMetadata(string operationId, string summary, string tag, string description)
        {
            return OpenApiRouteMetadata.Create(summary, tag)
                .WithOperationId(operationId)
                .WithDescription(description);
        }

        private static string BuildPreflightOperationId(string path)
        {
            if (String.Equals(path, "/", StringComparison.Ordinal))
                return "OptionsRoot";

            StringBuilder sb = new StringBuilder("Options");

            foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                string cleaned = segment.Trim('{', '}');
                if (String.IsNullOrWhiteSpace(cleaned))
                    continue;

                string[] parts = cleaned.Split(new[] { '-', '.', '_' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    if (part.Length < 1)
                        continue;

                    sb.Append(Char.ToUpperInvariant(part[0]));
                    if (part.Length > 1)
                        sb.Append(part.Substring(1));
                }
            }

            return sb.ToString();
        }

        private static IEnumerable<string> GetPathParameterNames(string path)
        {
            if (String.IsNullOrEmpty(path))
                yield break;

            int position = 0;
            while (position < path.Length)
            {
                int start = path.IndexOf('{', position);
                if (start < 0)
                    yield break;

                int end = path.IndexOf('}', start + 1);
                if (end < 0)
                    yield break;

                string name = path.Substring(start + 1, end - start - 1);
                if (!String.IsNullOrWhiteSpace(name))
                    yield return name;

                position = end + 1;
            }
        }

        private static OpenApiRouteMetadata CreateProtectedRouteMetadata(string operationId, string summary, string tag, string description)
        {
            return CreatePublicRouteMetadata(operationId, summary, tag, description)
                .RequireSecurity("ApiKey", "Bearer");
        }

        private static OpenApiResponseMetadata HtmlResponse(string description)
        {
            return new OpenApiResponseMetadata
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["text/html"] = new OpenApiMediaTypeMetadata
                    {
                        Schema = OpenApiSchemaMetadata.String()
                    }
                }
            };
        }

        private static OpenApiResponseMetadata JsonObjectResponse(string description)
        {
            return new OpenApiResponseMetadata
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaTypeMetadata>
                {
                    ["application/json"] = new OpenApiMediaTypeMetadata
                    {
                        Schema = new OpenApiSchemaMetadata { Type = "object" }
                    }
                }
            };
        }

        private static OpenApiResponseMetadata ErrorResponse(string description)
        {
            return OpenApiResponseMetadata.Json(description, OpenApiSchemaMetadata.CreateRef("ApiErrorResponse"));
        }

        private static string GetKeyName(ApiRequest req)
        {
            return req.Metadata?.ToString() ?? "unknown";
        }

        private static string GetSourceIp(ApiRequest req)
        {
            return GetSourceIp(req.Http);
        }

        private static string GetSourceIp(HttpContextBase ctx)
        {
            return ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port;
        }

        private static string GetMultipartBoundary(string contentType)
        {
            if (String.IsNullOrEmpty(contentType)) return null;
            if (!contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase)) return null;

            string[] parts = contentType.Split(';');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring(9).Trim('"');
                }
            }

            return null;
        }

        private static ParsedMultipartFile ParseMultipartFormData(byte[] bodyBytes, string boundary)
        {
            if (bodyBytes == null || bodyBytes.Length < 1) return null;

            byte[] boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
            byte[] headerSeparator = Encoding.UTF8.GetBytes("\r\n\r\n");
            byte[] crlf = Encoding.UTF8.GetBytes("\r\n");

            int position = IndexOf(bodyBytes, boundaryBytes, 0);
            if (position < 0) return null;

            position += boundaryBytes.Length;
            if (position + 2 <= bodyBytes.Length && bodyBytes[position] == 0x0D && bodyBytes[position + 1] == 0x0A)
                position += 2;

            int headerEnd = IndexOf(bodyBytes, headerSeparator, position);
            if (headerEnd < 0) return null;

            string headers = Encoding.UTF8.GetString(bodyBytes, position, headerEnd - position);

            string fileName = null;
            string fileContentType = Constants.BinaryContentType;

            fileName = ExtractHeaderParameter(headers, "filename*");
            if (!String.IsNullOrEmpty(fileName))
            {
                int encodingMarker = fileName.IndexOf("''", StringComparison.Ordinal);
                if (encodingMarker >= 0 && encodingMarker + 2 < fileName.Length)
                    fileName = fileName.Substring(encodingMarker + 2);
            }

            if (String.IsNullOrEmpty(fileName))
                fileName = ExtractHeaderParameter(headers, "filename");

            if (fileName == null) return null;

            if (headers.Contains("Content-Type:", StringComparison.OrdinalIgnoreCase))
            {
                int start = headers.IndexOf("Content-Type:", StringComparison.OrdinalIgnoreCase) + 13;
                int end = headers.IndexOf("\r\n", start, StringComparison.Ordinal);
                if (end < 0) end = headers.Length;
                fileContentType = headers.Substring(start, end - start).Trim();
            }

            int dataStart = headerEnd + headerSeparator.Length;
            int nextBoundary = IndexOf(bodyBytes, crlf.Concat(boundaryBytes).ToArray(), dataStart);
            int dataEnd = nextBoundary >= 0 ? nextBoundary : bodyBytes.Length;

            int dataLength = Math.Max(0, dataEnd - dataStart);
            MemoryStream fileStream = new MemoryStream(bodyBytes, dataStart, dataLength);

            return new ParsedMultipartFile
            {
                FileName = fileName,
                ContentType = fileContentType,
                FileStream = fileStream
            };
        }

        private static int IndexOf(byte[] haystack, byte[] needle, int startIndex)
        {
            if (needle.Length == 0 || haystack.Length == 0 || startIndex + needle.Length > haystack.Length)
                return -1;

            for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return i;
            }

            return -1;
        }

        private static string ExtractHeaderParameter(string headers, string parameterName)
        {
            if (String.IsNullOrEmpty(headers) || String.IsNullOrEmpty(parameterName))
                return null;

            string marker = parameterName + "=";
            int start = headers.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;

            start += marker.Length;
            if (start >= headers.Length) return null;

            if (headers[start] == '"')
            {
                start++;
                int end = headers.IndexOf('"', start);
                if (end > start)
                    return headers.Substring(start, end - start);

                return null;
            }

            int semicolon = headers.IndexOf(';', start);
            int newLine = headers.IndexOf("\r\n", start, StringComparison.Ordinal);
            int endIndex = headers.Length;

            if (semicolon >= 0 && semicolon < endIndex) endIndex = semicolon;
            if (newLine >= 0 && newLine < endIndex) endIndex = newLine;
            if (endIndex <= start) return null;

            return headers.Substring(start, endIndex - start).Trim();
        }

        private static string BuildDirectoryListing(Collection collection, List<CollectionFile> files)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("  <title>Index of /" + collection.Slug + "/</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 40px; background: #f8f9fa; color: #333; }");
            sb.AppendLine("    h1 { border-bottom: 1px solid #dee2e6; padding-bottom: 10px; color: #212529; }");
            sb.AppendLine("    table { border-collapse: collapse; width: 100%; background: #fff; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
            sb.AppendLine("    th, td { text-align: left; padding: 10px 16px; border-bottom: 1px solid #e9ecef; }");
            sb.AppendLine("    th { background: #f1f3f5; font-weight: 600; }");
            sb.AppendLine("    tr:hover { background: #f8f9fa; }");
            sb.AppendLine("    a { color: #0066cc; text-decoration: none; }");
            sb.AppendLine("    a:hover { text-decoration: underline; }");
            sb.AppendLine("    .footer { margin-top: 20px; font-size: 0.85em; color: #6c757d; }");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("  <h1>Index of /" + collection.Slug + "/</h1>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <tr><th>Name</th><th>Size</th><th>Last Modified</th></tr>");

            foreach (CollectionFile file in files.OrderBy(f => f.FileName))
            {
                string encodedFileName = Uri.EscapeDataString(file.FileName);
                string sizeStr = FormatFileSize(file.Size);
                string dateStr = file.LastModifiedUtc.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
                sb.AppendLine("    <tr>");
                sb.AppendLine("      <td><a href=\"/download/" + collection.Slug + "/" + encodedFileName + "\">" + System.Net.WebUtility.HtmlEncode(file.FileName) + "</a></td>");
                sb.AppendLine("      <td>" + sizeStr + "</td>");
                sb.AppendLine("      <td>" + dateStr + "</td>");
                sb.AppendLine("    </tr>");
            }

            sb.AppendLine("  </table>");
            sb.AppendLine("  <div class=\"footer\">" + collection.Name + " &mdash; " + files.Count + " file(s)</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private static string BuildSitemapXml(string slug, List<CollectionFile> files)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            foreach (CollectionFile file in files)
            {
                string encodedFileName = Uri.EscapeDataString(file.FileName);
                sb.AppendLine("  <url>");
                sb.AppendLine("    <loc>/download/" + slug + "/" + encodedFileName + "</loc>");
                sb.AppendLine("    <lastmod>" + file.LastModifiedUtc.ToString("yyyy-MM-dd") + "</lastmod>");
                sb.AppendLine("  </url>");
            }

            sb.AppendLine("</urlset>");
            return sb.ToString();
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F1") + " GB";
        }
    }
}
