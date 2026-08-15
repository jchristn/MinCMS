namespace MinCms.Test.Shared.Support
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using MinCms.Server;
    using SyslogLogging;

    /// <summary>
    /// Provides HTTP access to a MinCMS API host for integration test cases.
    /// A single shared host (backed by an in-memory collection service seeded with the
    /// "alpha" collection and "sample.txt") is started lazily and reused across cases.
    /// Read-only cases use the seeded data; mutating cases operate on their own uniquely
    /// named collections and files so they never disturb the shared seed.
    /// </summary>
    public static class ApiHost
    {
        /// <summary>API key accepted by the shared host.</summary>
        public const string ApiKey = "test-key";

        private static readonly Lazy<ApiHostContext> _Shared =
            new Lazy<ApiHostContext>(() => Create(null, new InMemoryCollectionService()));

        /// <summary>Shared HTTP client bound to the shared host.</summary>
        public static HttpClient Client => _Shared.Value.Client;

        /// <summary>Build a request carrying the valid x-api-key header.</summary>
        public static HttpRequestMessage Authorized(HttpMethod method, string path)
        {
            HttpRequestMessage request = new HttpRequestMessage(method, path);
            request.Headers.Add("x-api-key", ApiKey);
            return request;
        }

        /// <summary>
        /// Create an isolated host with custom settings and/or a custom collection service.
        /// The caller owns the returned context and must dispose it.
        /// </summary>
        public static ApiHostContext CreateIsolated(Action<ServerSettings> configure, ICollectionService service = null)
        {
            return Create(configure, service ?? new InMemoryCollectionService());
        }

        private static ApiHostContext Create(Action<ServerSettings> configure, ICollectionService service)
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
                    new AccessKeyEntry("Test", ApiKey)
                },
                Cors = new CorsSettings()
            };

            configure?.Invoke(settings);

            LoggingModule logging = new LoggingModule();
            logging.Settings.EnableConsole = false;

            MinCmsApiHost host = new MinCmsApiHost(settings, logging, service);
            host.StartAsync().GetAwaiter().GetResult();

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("http://127.0.0.1:" + port)
            };

            return new ApiHostContext(host, client);
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

    /// <summary>
    /// Disposable wrapper around a running MinCMS API host and its HTTP client.
    /// </summary>
    public sealed class ApiHostContext : IDisposable
    {
        private readonly MinCmsApiHost _Host;

        /// <summary>HTTP client bound to the host.</summary>
        public HttpClient Client { get; }

        /// <summary>Instantiate.</summary>
        public ApiHostContext(MinCmsApiHost host, HttpClient client)
        {
            _Host = host;
            Client = client;
        }

        /// <summary>Dispose the client and host.</summary>
        public void Dispose()
        {
            Client.Dispose();
            _Host.Dispose();
        }
    }
}
