namespace MinCms.Server
{
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Core.Serialization;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using SwiftStack;
    using SwiftStack.Rest;
    using SwiftStack.Rest.OpenApi;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;
    using ApiErrorResponse = MinCms.Core.ApiErrorResponse;
    using Constants = MinCms.Core.Constants;
    using DownloadFileResult = MinCms.Core.DownloadFileResult;
    using Serializer = MinCms.Core.Serialization.Serializer;

    /// <summary>
    /// MinCMS server main class.
    /// </summary>
    public static class MinCmsServer
    {
        private static string _Header = "[MinCmsServer] ";
        private static int _ProcessId = Environment.ProcessId;
        private static LoggingModule _Logging = null;
        private static ServerSettings _Settings = null;
        private static Serializer _Serializer = new Serializer();
        private static SwiftStackApp _App = null;
        private static IS3Service _S3Service = null;
        private static ICollectionService _CollectionService = null;
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private static bool _ShuttingDown = false;

        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <returns>Task.</returns>
        public static async Task Main(string[] args)
        {
            Welcome();
            InitializeSettings();
            ApplyEnvironmentOverrides();
            InitializeLogging();
            await InitializeServicesAsync().ConfigureAwait(false);
            InitializeWebserver();

            _Logging.Info(_Header + "starting at " + DateTime.UtcNow + " using process ID " + _ProcessId);

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            AssemblyLoadContext.Default.Unloading += (ctx) => waitHandle.Set();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;

                if (!_ShuttingDown)
                {
                    Console.WriteLine();
                    Console.WriteLine("Shutting down");
                    _TokenSource.Cancel();
                    _ShuttingDown = true;
                    waitHandle.Set();
                }
            };

            bool waitHandleSignal = false;
            do
            {
                waitHandleSignal = waitHandle.WaitOne(1000);
            }
            while (!waitHandleSignal);

            _Logging.Info(_Header + "stopping at " + DateTime.UtcNow);
        }

        private static void Welcome()
        {
            Console.WriteLine(
                Environment.NewLine +
                Constants.Logo +
                Environment.NewLine +
                Constants.ProductName +
                Environment.NewLine +
                Constants.Copyright +
                Environment.NewLine);
        }

        private static void InitializeSettings()
        {
            Console.WriteLine("Using settings file '" + Constants.SettingsFile + "'");

            if (!File.Exists(Constants.SettingsFile))
            {
                _Settings = new ServerSettings();
                Console.WriteLine("Creating settings file '" + Constants.SettingsFile + "' with default configuration");
                File.WriteAllBytes(Constants.SettingsFile, Encoding.UTF8.GetBytes(_Serializer.SerializeJson(_Settings, true)));
                Console.WriteLine("");
                Console.WriteLine("Please modify mincms.json to specify your S3 bucket access material and other configuration items.");
                Environment.Exit(1);
                return;
            }
            else
            {
                _Settings = _Serializer.DeserializeJson<ServerSettings>(File.ReadAllText(Constants.SettingsFile));
            }
        }

        private static void ApplyEnvironmentOverrides()
        {
            string val;

            // S3 overrides
            val = Environment.GetEnvironmentVariable(Constants.S3AccessKeyEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.S3.AccessKey = val;

            val = Environment.GetEnvironmentVariable(Constants.S3SecretKeyEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.S3.SecretKey = val;

            val = Environment.GetEnvironmentVariable(Constants.S3BucketEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.S3.Bucket = val;

            val = Environment.GetEnvironmentVariable(Constants.S3RegionEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.S3.Region = val;

            val = Environment.GetEnvironmentVariable(Constants.S3EndpointEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.S3.EndpointUrl = val;

            val = Environment.GetEnvironmentVariable(Constants.S3UseSslEnvVar);
            if (!String.IsNullOrEmpty(val) && Boolean.TryParse(val, out bool useSsl))
                _Settings.S3.UseSsl = useSsl;

            val = Environment.GetEnvironmentVariable(Constants.S3RequestStyleEnvVar);
            if (!String.IsNullOrEmpty(val) && Enum.TryParse<MinCms.Core.Settings.S3RequestStyle>(val, true, out MinCms.Core.Settings.S3RequestStyle requestStyle))
                _Settings.S3.RequestStyle = requestStyle;

            // Webserver overrides
            val = Environment.GetEnvironmentVariable(Constants.WebserverHostnameEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.Rest.Hostname = val;

            val = Environment.GetEnvironmentVariable(Constants.WebserverPortEnvVar);
            if (!String.IsNullOrEmpty(val) && Int32.TryParse(val, out int port))
                _Settings.Rest.Port = port;
        }

        private static void InitializeLogging()
        {
            Console.WriteLine("Initializing logging");

            List<SyslogLogging.SyslogServer> syslogServers = new List<SyslogLogging.SyslogServer>();

            if (_Settings.Logging.Servers != null && _Settings.Logging.Servers.Count > 0)
            {
                foreach (SyslogServerSettings server in _Settings.Logging.Servers)
                {
                    syslogServers.Add(new SyslogLogging.SyslogServer(server.Hostname, server.Port));
                    Console.WriteLine("| syslog://" + server.Hostname + ":" + server.Port);
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.MinimumSeverity = (SyslogLogging.Severity)_Settings.Logging.MinimumSeverity;
            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;

            if (_Settings.Logging.FileLogging
                && !String.IsNullOrEmpty(_Settings.Logging.LogDirectory)
                && !String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                if (!Directory.Exists(_Settings.Logging.LogDirectory))
                    Directory.CreateDirectory(_Settings.Logging.LogDirectory);

                _Logging.Settings.LogFilename = Path.Combine(_Settings.Logging.LogDirectory, _Settings.Logging.LogFilename);

                if (_Settings.Logging.IncludeDateInFilename)
                    _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                else
                    _Logging.Settings.FileLogging = FileLoggingMode.SingleLogFile;
            }

            _Logging.Info(_Header + "logging initialized");
        }

        private static async Task InitializeServicesAsync()
        {
            Console.WriteLine("Initializing services");

            _S3Service = new S3Service(_Settings.S3, _Logging);
            _CollectionService = new CollectionService(_S3Service, _Logging);

            await _S3Service.EnsureCollectionsConfigExistsAsync(_TokenSource.Token).ConfigureAwait(false);

            _Logging.Info(_Header + "services initialized");
        }

        private static void InitializeWebserver()
        {
            string scheme = _Settings.Rest.Ssl ? "https" : "http";
            string prefix = scheme + "://" + _Settings.Rest.Hostname + ":" + _Settings.Rest.Port + "/";
            Console.WriteLine("Initializing webserver on " + prefix);

            _App = new SwiftStackApp("MinCMS");
            _App.Serializer = new MinCmsSerializer();
            _App.Rest.WebserverSettings.Hostname = _Settings.Rest.Hostname;
            _App.Rest.WebserverSettings.Port = _Settings.Rest.Port;
            _App.Rest.WebserverSettings.Ssl.Enable = _Settings.Rest.Ssl;

            #region OpenAPI

            _App.Rest.UseOpenApi(openApi =>
            {
                openApi.Info.Title = "MinCMS API";
                openApi.Info.Version = "1.0.0";
                openApi.Info.Description = "MinCMS API";

                openApi.Tags.Add(new OpenApiTag("Health", "Health check endpoints"));
                openApi.Tags.Add(new OpenApiTag("Collections", "Collection management"));
                openApi.Tags.Add(new OpenApiTag("Files", "File management within collections"));
                openApi.Tags.Add(new OpenApiTag("Downloads", "Public file download endpoints"));

                openApi.SecuritySchemes["ApiKey"] = OpenApiSecurityScheme.ApiKey(Constants.ApiKeyHeader, "header", "API key provided in x-api-key header");
                openApi.SecuritySchemes["Bearer"] = OpenApiSecurityScheme.Bearer("Bearer", "API key provided as Bearer token in Authorization header");
            });

            #endregion

            #region Pre-Post-Routing

            _App.Rest.PreRoutingRoute = async (ctx) =>
            {
                ctx.Response.ContentType = Constants.JsonContentType;
            };

            _App.Rest.PostRoutingRoute = async (ctx) =>
            {
                ctx.Timestamp.End = DateTime.UtcNow;

                _Logging.Debug(
                    _Header
                    + ctx.Request.Method + " " + ctx.Request.Url.RawWithQuery + " "
                    + ctx.Response.StatusCode + " "
                    + "(" + ctx.Response.Timestamp.TotalMs.Value.ToString("F2") + "ms) "
                    + "from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);
            };

            #endregion

            #region Exception-Handling

            _App.Rest.ExceptionRoute = async (ctx, e) =>
            {
                switch (e)
                {
                    #region Status-400

                    case ArgumentOutOfRangeException:
                        _Logging.Warn(_Header + "argument out of range exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case ArgumentNullException:
                        _Logging.Warn(_Header + "argument null exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case ArgumentException:
                        _Logging.Warn(_Header + "argument exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case FormatException:
                        _Logging.Warn(_Header + "format exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case JsonException:
                        _Logging.Warn(_Header + "JSON exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 400;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, e.Message), true)).ConfigureAwait(false);
                        return;

                    #endregion

                    #region Status-404

                    case FileNotFoundException:
                        _Logging.Warn(_Header + "file not found exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 404;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.NotFound, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case KeyNotFoundException:
                        _Logging.Warn(_Header + "key not found exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 404;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.NotFound, null, e.Message), true)).ConfigureAwait(false);
                        return;

                    #endregion

                    #region Status-409

                    case InvalidOperationException:
                        _Logging.Warn(_Header + "invalid operation exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 409;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.Conflict, null, e.Message), true)).ConfigureAwait(false);
                        return;

                    #endregion

                    #region Status-503

                    case TaskCanceledException:
                        _Logging.Warn(_Header + "task canceled exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.Timeout, null, e.Message), true)).ConfigureAwait(false);
                        return;
                    case OperationCanceledException:
                        _Logging.Warn(_Header + "operation canceled exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 503;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.Timeout, null, e.Message), true)).ConfigureAwait(false);
                        return;

                    #endregion

                    #region Status-500

                    default:
                        _Logging.Warn(_Header + "exception:" + Environment.NewLine + e.ToString());
                        ctx.Response.StatusCode = 500;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.InternalError, null, e.Message), true)).ConfigureAwait(false);
                        return;

                    #endregion
                }
            };

            #endregion

            #region Unauthenticated-Health

            _App.Rest.Head("/", async (req) => "",
                openApi: api => api.WithTag("Health").WithSummary("Health check"));

            _App.Rest.Get("/", async (req) =>
            {
                req.Http.Response.ContentType = Constants.HtmlContentType;
                return Constants.HtmlHomepage;
            },
                openApi: api => api.WithTag("Health").WithSummary("API root"));

            #endregion

            #region Authentication

            _App.Rest.AuthenticationRoute = async (ctx) =>
            {
                bool readOnlyMethod =
                    ctx.Request.Method == WatsonWebserver.Core.HttpMethod.OPTIONS
                    || ctx.Request.Method == WatsonWebserver.Core.HttpMethod.GET
                    || ctx.Request.Method == WatsonWebserver.Core.HttpMethod.HEAD;

                string apiKey = null;

                if (ctx.Request.Headers.AllKeys.Contains(Constants.ApiKeyHeader))
                {
                    apiKey = ctx.Request.Headers.Get(Constants.ApiKeyHeader);
                }
                else if (ctx.Request.Headers.AllKeys.Contains(Constants.AuthorizationHeader))
                {
                    string authHeader = ctx.Request.Headers.Get(Constants.AuthorizationHeader);
                    if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        apiKey = authHeader.Substring(7).Trim();
                    }
                }

                if (String.IsNullOrEmpty(apiKey))
                {
                    if (!readOnlyMethod)
                    {
                        _Logging.Warn(_Header + "no auth material supplied for " + ctx.Request.Method + " from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed, null, "Authentication required."), true)).ConfigureAwait(false);
                    }

                    return new AuthResult { AuthenticationResult = AuthenticationResultEnum.Invalid };
                }

                AccessKeyEntry matchedKey = _Settings.AccessKeys.FirstOrDefault(k => String.Equals(k.Key, apiKey, StringComparison.Ordinal));
                if (matchedKey == null)
                {
                    _Logging.Warn(_Header + "API key from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " not found");

                    if (!readOnlyMethod)
                    {
                        ctx.Response.StatusCode = 401;
                        ctx.Response.ContentType = Constants.JsonContentType;
                        await ctx.Response.Send(_Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.AuthenticationFailed, null, "Authentication required."), true)).ConfigureAwait(false);
                    }

                    return new AuthResult { AuthenticationResult = AuthenticationResultEnum.NotFound };
                }

                _Logging.Debug(_Header + "authenticated key '" + matchedKey.Name + "' from " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port);
                return new AuthResult
                {
                    AuthenticationResult = AuthenticationResultEnum.Success,
                    Metadata = matchedKey.Name
                };
            };

            #endregion

            #region Collection-Management

            _App.Rest.Get("/v1.0/collections", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;

                _Logging.Info(_Header + "list collections | key=" + keyName + " | source=" + sourceIp);

                List<Collection> collections = await _CollectionService.GetAllCollectionsAsync(_TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "list collections | key=" + keyName + " | source=" + sourceIp + " | result=Success | count=" + collections.Count);

                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(collections, true);
            },
                openApi: api => api.WithTag("Collections").WithSummary("List all collections"),
                requireAuthentication: true);

            _App.Rest.Post<Collection>("/v1.0/collections", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;

                Collection input = req.GetData<Collection>();

                _Logging.Info(_Header + "create collection | key=" + keyName + " | source=" + sourceIp + " | name=" + input.Name + " | slug=" + input.Slug);

                Collection created = await _CollectionService.CreateCollectionAsync(input.Name, input.Slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "create collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + created.Slug + " | result=Success");

                req.Http.Response.StatusCode = 201;
                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(created, true);
            },
                openApi: api => api.WithTag("Collections").WithSummary("Create a new collection"),
                requireAuthentication: true);

            _App.Rest.Get("/v1.0/collections/{slug}", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                _Logging.Info(_Header + "get collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "get collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success");

                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(collection, true);
            },
                openApi: api => api.WithTag("Collections").WithSummary("Get collection by slug"),
                requireAuthentication: true);

            _App.Rest.Delete("/v1.0/collections/{slug}", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                _Logging.Info(_Header + "delete collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                await _CollectionService.DeleteCollectionAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "delete collection | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success");

                req.Http.Response.StatusCode = 204;
                return "";
            },
                openApi: api => api.WithTag("Collections").WithSummary("Delete collection and all its files"),
                requireAuthentication: true);

            #endregion

            #region File-Management

            _App.Rest.Get("/v1.0/collections/{slug}/files", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                _Logging.Info(_Header + "list files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug);

                List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "list files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success | count=" + files.Count);

                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(files, true);
            },
                openApi: api => api.WithTag("Files").WithSummary("List files in a collection"),
                requireAuthentication: true);

            _App.Rest.Route("POST", "/v1.0/collections/{slug}/files", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                string contentType = req.Http.Request.ContentType;
                string boundary = GetMultipartBoundary(contentType);

                if (String.IsNullOrEmpty(boundary))
                {
                    req.Http.Response.StatusCode = 400;
                    return _Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, "Request must be multipart/form-data."), true);
                }

                ParsedMultipartFile parsedFile = ParseMultipartFormData(req.Http.Request.DataAsBytes, boundary);

                if (parsedFile == null || String.IsNullOrEmpty(parsedFile.FileName) || parsedFile.FileStream == null)
                {
                    req.Http.Response.StatusCode = 400;
                    return _Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, "No file found in multipart form data."), true);
                }

                string decodedFileName = Uri.UnescapeDataString(parsedFile.FileName);

                _Logging.Debug(_Header + "upload file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + decodedFileName);

                await _CollectionService.UploadFileAsync(slug, decodedFileName, parsedFile.FileStream, parsedFile.ContentType, _TokenSource.Token).ConfigureAwait(false);

                CollectionFile metadata = await _CollectionService.GetFileMetadataAsync(slug, decodedFileName, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "upload file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + decodedFileName + " | result=Success");

                req.Http.Response.StatusCode = 201;
                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(metadata, true);
            }, null, true);

            _App.Rest.Get("/v1.0/collections/{slug}/files/{fileName}", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];
                string fileName = Uri.UnescapeDataString(req.Parameters["fileName"]);

                _Logging.Info(_Header + "get file metadata | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                CollectionFile metadata = await _CollectionService.GetFileMetadataAsync(slug, fileName, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "get file metadata | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success");

                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(metadata, true);
            },
                openApi: api => api.WithTag("Files").WithSummary("Get file metadata"),
                requireAuthentication: true);

            _App.Rest.Route("DELETE", "/v1.0/collections/{slug}/files", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                string body = Encoding.UTF8.GetString(req.Http.Request.DataAsBytes);
                DeleteFilesRequest deleteRequest = _Serializer.DeserializeJson<DeleteFilesRequest>(body);

                if (deleteRequest == null || deleteRequest.FileNames == null || deleteRequest.FileNames.Count == 0)
                {
                    req.Http.Response.StatusCode = 400;
                    req.Http.Response.ContentType = Constants.JsonContentType;
                    return _Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.BadRequest, null, "At least one filename is required in FileNames."), true);
                }

                _Logging.Info(_Header + "batch delete files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | count=" + deleteRequest.FileNames.Count);

                int deletedCount = await _CollectionService.DeleteFilesAsync(slug, deleteRequest.FileNames, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "batch delete files | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | result=Success | deleted=" + deletedCount);

                req.Http.Response.ContentType = Constants.JsonContentType;
                return _Serializer.SerializeJson(new DeleteFilesResponse { DeletedCount = deletedCount }, true);
            }, null, true);

            _App.Rest.Delete("/v1.0/collections/{slug}/files/{fileName}", async (req) =>
            {
                string keyName = GetKeyName(req);
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];
                string fileName = Uri.UnescapeDataString(req.Parameters["fileName"]);

                _Logging.Info(_Header + "delete file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                await _CollectionService.DeleteFileAsync(slug, fileName, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "delete file | key=" + keyName + " | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success");

                req.Http.Response.StatusCode = 204;
                return "";
            },
                openApi: api => api.WithTag("Files").WithSummary("Delete a file"),
                requireAuthentication: true);

            #endregion

            #region Public-Downloads

            _App.Rest.Get("/download/{slug}", async (req) =>
            {
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                _Logging.Info(_Header + "browse collection | key=anonymous | source=" + sourceIp + " | slug=" + slug);

                Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                if (!collection.IsActive)
                {
                    req.Http.Response.StatusCode = 404;
                    return _Serializer.SerializeJson(new ApiErrorResponse(ApiErrorEnum.NotFound, null, "Collection is not active."), true);
                }

                List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "browse collection | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | result=Success | count=" + files.Count);

                req.Http.Response.ContentType = Constants.HtmlContentType;
                return BuildDirectoryListing(collection, files);
            },
                openApi: api => api.WithTag("Downloads").WithSummary("Browse collection files (HTML listing)"));

            _App.Rest.Get("/download/{slug}/sitemap.xml", async (req) =>
            {
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];

                _Logging.Info(_Header + "sitemap | key=anonymous | source=" + sourceIp + " | slug=" + slug);

                Collection collection = await _CollectionService.GetCollectionBySlugAsync(slug, _TokenSource.Token).ConfigureAwait(false);
                List<CollectionFile> files = await _CollectionService.GetCollectionFilesAsync(slug, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "sitemap | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | result=Success");

                req.Http.Response.ContentType = Constants.XmlContentType;
                return BuildSitemapXml(slug, files);
            },
                openApi: api => api.WithTag("Downloads").WithSummary("Collection sitemap XML"));

            _App.Rest.Route("GET", "/download/{slug}/{fileName}", async (req) =>
            {
                string sourceIp = req.Http.Request.Source.IpAddress + ":" + req.Http.Request.Source.Port;
                string slug = req.Parameters["slug"];
                string fileName = Uri.UnescapeDataString(req.Parameters["fileName"]);

                _Logging.Info(_Header + "download | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName);

                DownloadFileResult downloadResult =
                    await _CollectionService.DownloadFileAsync(slug, fileName, _TokenSource.Token).ConfigureAwait(false);

                _Logging.Info(_Header + "download | key=anonymous | source=" + sourceIp + " | slug=" + slug + " | file=" + fileName + " | result=Success | size=" + downloadResult.ContentLength);

                req.Http.Response.ContentType = downloadResult.ContentType;
                req.Http.Response.StatusCode = 200;
                req.Http.Response.ContentLength = downloadResult.ContentLength;
                string downloadName = downloadResult.FileName.Contains("/") ? downloadResult.FileName.Substring(downloadResult.FileName.LastIndexOf('/') + 1) : downloadResult.FileName;
                req.Http.Response.Headers.Add("Content-Disposition", "attachment; filename=\"" + downloadName + "\"");

                byte[] fileBytes;
                using (MemoryStream ms = new MemoryStream())
                {
                    await downloadResult.Content.CopyToAsync(ms).ConfigureAwait(false);
                    fileBytes = ms.ToArray();
                }

                downloadResult.Content.Dispose();
                await req.Http.Response.Send(fileBytes).ConfigureAwait(false);
                return null;
            }, null, false);

            #endregion

            Task.Run(() => _App.Rest.Run(_TokenSource.Token), _TokenSource.Token);

            _Logging.Info(_Header + "webserver initialized");
        }

        #region Private-Helpers

        private static string GetKeyName(AppRequest req)
        {
            if (req.AuthResult != null && req.AuthResult.Metadata != null)
                return req.AuthResult.Metadata.ToString();
            return "unknown";
        }

        private static string GetMultipartBoundary(string contentType)
        {
            if (String.IsNullOrEmpty(contentType)) return null;
            if (!contentType.Contains("multipart/form-data")) return null;

            string[] parts = contentType.Split(';');
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                {
                    string boundary = trimmed.Substring(9).Trim('"');
                    return boundary;
                }
            }

            return null;
        }

        private static ParsedMultipartFile ParseMultipartFormData(byte[] bodyBytes, string boundary)
        {
            if (bodyBytes == null || bodyBytes.Length == 0) return null;

            // Work directly with bytes to avoid corrupting binary data via UTF-8 round-trip
            byte[] boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
            byte[] headerSep = Encoding.UTF8.GetBytes("\r\n\r\n");
            byte[] crlf = Encoding.UTF8.GetBytes("\r\n");

            // Find the first boundary
            int pos = IndexOf(bodyBytes, boundaryBytes, 0);
            if (pos < 0) return null;

            // Move past the boundary and the CRLF that follows it
            pos += boundaryBytes.Length;
            if (pos + 2 <= bodyBytes.Length && bodyBytes[pos] == 0x0D && bodyBytes[pos + 1] == 0x0A)
                pos += 2;

            // Find the end of headers (\r\n\r\n)
            int headerEndPos = IndexOf(bodyBytes, headerSep, pos);
            if (headerEndPos < 0) return null;

            // Parse headers as UTF-8 text (headers are always ASCII-safe)
            string headers = Encoding.UTF8.GetString(bodyBytes, pos, headerEndPos - pos);

            string fileName = null;
            string fileContentType = Constants.BinaryContentType;

            if (headers.Contains("filename="))
            {
                int fnStart = headers.IndexOf("filename=\"") + 10;
                int fnEnd = headers.IndexOf("\"", fnStart);
                if (fnStart > 10 && fnEnd > fnStart)
                {
                    fileName = headers.Substring(fnStart, fnEnd - fnStart);
                }
            }

            if (fileName == null) return null;

            if (headers.Contains("Content-Type:"))
            {
                int ctStart = headers.IndexOf("Content-Type:") + 13;
                int ctEnd = headers.IndexOf("\r\n", ctStart);
                if (ctEnd < 0) ctEnd = headers.Length;
                fileContentType = headers.Substring(ctStart, ctEnd - ctStart).Trim();
            }

            // File data starts right after \r\n\r\n
            int dataStart = headerEndPos + headerSep.Length;

            // Find the next boundary to determine where file data ends
            // The closing boundary is \r\n--boundary-- but we search for \r\n--boundary
            int nextBoundary = IndexOf(bodyBytes, crlf.Concat(boundaryBytes).ToArray(), dataStart);
            int dataEnd = nextBoundary >= 0 ? nextBoundary : bodyBytes.Length;

            int dataLength = dataEnd - dataStart;
            if (dataLength < 0) dataLength = 0;

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

        #endregion
    }
}
