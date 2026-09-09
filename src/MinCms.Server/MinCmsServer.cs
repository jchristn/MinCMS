namespace MinCms.Server
{
    using MinCms.Core;
    using MinCms.Core.Services;
    using MinCms.Core.Settings;
    using MinCms.Server.Telemetry;
    using Radiant;
    using SyslogLogging;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Serializer = MinCms.Core.Serialization.Serializer;

    /// <summary>
    /// MinCMS server main class.
    /// </summary>
    public static class MinCmsServer
    {
        private static readonly string _Header = "[MinCmsServer] ";
        private static readonly int _ProcessId = Environment.ProcessId;
        private static readonly Serializer _Serializer = new Serializer();
        private static readonly CancellationTokenSource _TokenSource = new CancellationTokenSource();

        private static LoggingModule _Logging = null;
        private static ServerSettings _Settings = null;
        private static RadiantHost _Telemetry = null;
        private static IS3Service _S3Service = null;
        private static ICollectionService _CollectionService = null;
        private static MinCmsApiHost _Host = null;
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
            InitializeTelemetry();
            await InitializeServicesAsync().ConfigureAwait(false);

            using (_Host = new MinCmsApiHost(_Settings, _Logging, _CollectionService))
            {
                Console.WriteLine("Initializing webserver on " + _Host.Prefix);
                await _Host.StartAsync(_TokenSource.Token).ConfigureAwait(false);

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

                bool waitHandleSignal;
                do
                {
                    waitHandleSignal = waitHandle.WaitOne(1000);
                }
                while (!waitHandleSignal);

                _Logging.Info(_Header + "stopping at " + DateTime.UtcNow);
            }

            _Telemetry?.Dispose();
        }

        private static void Welcome()
        {
            Console.WriteLine(
                Environment.NewLine
                + Constants.Logo
                + Environment.NewLine
                + Constants.ProductName
                + Environment.NewLine
                + Constants.Copyright
                + Environment.NewLine);
        }

        private static void InitializeSettings()
        {
            Console.WriteLine("Using settings file '" + Constants.SettingsFile + "'");

            if (!File.Exists(Constants.SettingsFile))
            {
                _Settings = new ServerSettings();
                Console.WriteLine("Creating settings file '" + Constants.SettingsFile + "' with default configuration");
                File.WriteAllBytes(Constants.SettingsFile, Encoding.UTF8.GetBytes(_Serializer.SerializeJson(_Settings, true)));
                Console.WriteLine();
                Console.WriteLine("Please modify mincms.json to specify your S3 bucket access material and other configuration items.");
                Environment.Exit(1);
                return;
            }

            _Settings = _Serializer.DeserializeJson<ServerSettings>(File.ReadAllText(Constants.SettingsFile));
        }

        private static void ApplyEnvironmentOverrides()
        {
            string val = Environment.GetEnvironmentVariable(Constants.S3AccessKeyEnvVar);
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
            if (!String.IsNullOrEmpty(val) && Enum.TryParse(val, true, out S3RequestStyle requestStyle))
                _Settings.S3.RequestStyle = requestStyle;

            val = Environment.GetEnvironmentVariable(Constants.WebserverHostnameEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.Rest.Hostname = val;

            val = Environment.GetEnvironmentVariable(Constants.WebserverPortEnvVar);
            if (!String.IsNullOrEmpty(val) && Int32.TryParse(val, out int port))
                _Settings.Rest.Port = port;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryEnabledEnvVar);
            if (!String.IsNullOrEmpty(val) && Boolean.TryParse(val, out bool telemetryEnabled))
                _Settings.Telemetry.Enable = telemetryEnabled;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryServiceNameEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.Telemetry.ServiceName = val;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryOtlpEnabledEnvVar);
            if (!String.IsNullOrEmpty(val) && Boolean.TryParse(val, out bool otlpEnabled))
                _Settings.Telemetry.Otlp.Enable = otlpEnabled;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryOtlpEndpointEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.Telemetry.Otlp.Endpoint = val;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryOtlpProtocolEnvVar);
            if (!String.IsNullOrEmpty(val)) _Settings.Telemetry.Otlp.Protocol = val;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryPrometheusEnabledEnvVar);
            if (!String.IsNullOrEmpty(val) && Boolean.TryParse(val, out bool prometheusEnabled))
                _Settings.Telemetry.Prometheus.Enable = prometheusEnabled;

            val = Environment.GetEnvironmentVariable(Constants.TelemetryPrometheusPortEnvVar);
            if (!String.IsNullOrEmpty(val) && Int32.TryParse(val, out int prometheusPort))
                _Settings.Telemetry.Prometheus.Port = prometheusPort;
        }

        private static void InitializeLogging()
        {
            Console.WriteLine("Initializing logging");

            List<SyslogServer> syslogServers = new List<SyslogServer>();
            if (_Settings.Logging.Servers != null && _Settings.Logging.Servers.Count > 0)
            {
                foreach (SyslogServerSettings server in _Settings.Logging.Servers)
                {
                    syslogServers.Add(new SyslogServer(server.Hostname, server.Port));
                    Console.WriteLine("| syslog://" + server.Hostname + ":" + server.Port);
                }
            }

            _Logging = syslogServers.Count > 0 ? new LoggingModule(syslogServers) : new LoggingModule();
            _Logging.Settings.MinimumSeverity = (Severity)_Settings.Logging.MinimumSeverity;
            _Logging.Settings.EnableConsole = _Settings.Logging.ConsoleLogging;
            _Logging.Settings.EnableColors = _Settings.Logging.EnableColors;

            if (_Settings.Logging.FileLogging
                && !String.IsNullOrEmpty(_Settings.Logging.LogDirectory)
                && !String.IsNullOrEmpty(_Settings.Logging.LogFilename))
            {
                if (!Directory.Exists(_Settings.Logging.LogDirectory))
                    Directory.CreateDirectory(_Settings.Logging.LogDirectory);

                _Logging.Settings.LogFilename = Path.Combine(_Settings.Logging.LogDirectory, _Settings.Logging.LogFilename);
                _Logging.Settings.FileLogging =
                    _Settings.Logging.IncludeDateInFilename
                    ? FileLoggingMode.FileWithDate
                    : FileLoggingMode.SingleLogFile;
            }

            _Logging.Info(_Header + "logging initialized");
        }

        private static void InitializeTelemetry()
        {
            Console.WriteLine("Initializing telemetry");
            _Telemetry = TelemetryBootstrap.Start(_Settings.Telemetry, _Logging);
        }

        private static async Task InitializeServicesAsync()
        {
            Console.WriteLine("Initializing services");

            _S3Service = new S3Service(_Settings.S3, _Logging);
            _CollectionService = new CollectionService(_S3Service, _Logging);

            await _S3Service.EnsureCollectionsConfigExistsAsync(_TokenSource.Token).ConfigureAwait(false);

            _Logging.Info(_Header + "services initialized");
        }
    }
}
