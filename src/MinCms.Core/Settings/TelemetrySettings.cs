namespace MinCms.Core.Settings
{
    using System;

    /// <summary>
    /// Telemetry (metrics, traces, logs) settings. These map onto a Radiant host at startup, which
    /// turns them into a wired OpenTelemetry pipeline exporting over OTLP and, optionally, an
    /// in-process Prometheus scrape endpoint. Emitting telemetry rides the .NET base class library
    /// (Meter / ActivitySource), so the instruments stay a no-op until the host subscribes.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch for the whole telemetry pipeline. Default true. When false, no providers are
        /// built, no ports are bound, and instrument emit stays a no-op.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The logical service name stamped as the <c>service.name</c> resource attribute. Default
        /// "MinCMS".
        /// </summary>
        public string ServiceName
        {
            get => _ServiceName;
            set => _ServiceName = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(ServiceName)));
        }

        /// <summary>
        /// The service instance identifier stamped as <c>service.instance.id</c>. Default null, which
        /// causes the host to generate a stable identifier for the process lifetime.
        /// </summary>
        public string ServiceInstanceId { get; set; } = null;

        /// <summary>
        /// OTLP push exporter settings (the path to an OpenTelemetry Collector).
        /// </summary>
        public OtlpTelemetrySettings Otlp
        {
            get => _Otlp;
            set => _Otlp = value ?? new OtlpTelemetrySettings();
        }

        /// <summary>
        /// In-process Prometheus scrape endpoint settings. Off by default; enable it to let a
        /// Prometheus server scrape the process directly without a collector.
        /// </summary>
        public PrometheusTelemetrySettings Prometheus
        {
            get => _Prometheus;
            set => _Prometheus = value ?? new PrometheusTelemetrySettings();
        }

        /// <summary>
        /// The metric export interval in milliseconds for the periodic OTLP reader. Default 15000.
        /// </summary>
        public int MetricsExportIntervalMs
        {
            get => _MetricsExportIntervalMs;
            set => _MetricsExportIntervalMs = (value >= 1000 && value <= 300000 ? value : throw new ArgumentOutOfRangeException(nameof(MetricsExportIntervalMs)));
        }

        /// <summary>
        /// Whether to include .NET runtime instrumentation (GC, heap, threads, JIT). Default true.
        /// </summary>
        public bool IncludeRuntimeMetrics { get; set; } = true;

        /// <summary>
        /// Whether to include baseline process metrics (working set, uptime, thread count). Default true.
        /// </summary>
        public bool IncludeProcessMetrics { get; set; } = true;

        /// <summary>
        /// The head-based trace sampling ratio in the range 0.0 to 1.0. Default 1.0 (sample all).
        /// </summary>
        public double TraceSamplingRatio
        {
            get => _TraceSamplingRatio;
            set => _TraceSamplingRatio = (value >= 0.0 && value <= 1.0 ? value : throw new ArgumentOutOfRangeException(nameof(TraceSamplingRatio)));
        }

        #endregion

        #region Private-Members

        private string _ServiceName = "MinCMS";
        private OtlpTelemetrySettings _Otlp = new OtlpTelemetrySettings();
        private PrometheusTelemetrySettings _Prometheus = new PrometheusTelemetrySettings();
        private int _MetricsExportIntervalMs = 15000;
        private double _TraceSamplingRatio = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion
    }

    /// <summary>
    /// OTLP push exporter settings.
    /// </summary>
    public class OtlpTelemetrySettings
    {
        /// <summary>
        /// Whether the OTLP push exporter is enabled. Default true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// The collector endpoint. Default <c>http://localhost:4317</c> (gRPC). Use
        /// <c>http://localhost:4318</c> for the <c>HttpProtobuf</c> protocol.
        /// </summary>
        public string Endpoint
        {
            get => _Endpoint;
            set => _Endpoint = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Endpoint)));
        }

        /// <summary>
        /// The OTLP wire protocol: <c>Grpc</c> (default) or <c>HttpProtobuf</c>.
        /// </summary>
        public string Protocol
        {
            get => _Protocol;
            set => _Protocol = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Protocol)));
        }

        /// <summary>
        /// The per-export timeout in milliseconds. Default 10000.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = (value >= 1000 && value <= 120000 ? value : throw new ArgumentOutOfRangeException(nameof(TimeoutMs)));
        }

        private string _Endpoint = "http://localhost:4317";
        private string _Protocol = "Grpc";
        private int _TimeoutMs = 10000;
    }

    /// <summary>
    /// In-process Prometheus scrape endpoint settings.
    /// </summary>
    public class PrometheusTelemetrySettings
    {
        /// <summary>
        /// Whether the in-process scrape endpoint is enabled. Default false.
        /// </summary>
        public bool Enable { get; set; } = false;

        /// <summary>
        /// The hostname to bind. Default <c>localhost</c>. Use <c>*</c> or <c>+</c> to bind all
        /// interfaces (requires an HTTP namespace reservation on Windows).
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Hostname)));
        }

        /// <summary>
        /// The TCP port to bind. Default 9464 (the OpenTelemetry Prometheus convention).
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value >= 1 && value <= 65535 ? value : throw new ArgumentOutOfRangeException(nameof(Port)));
        }

        /// <summary>
        /// The scrape path. Default <c>/metrics</c>.
        /// </summary>
        public string Path
        {
            get => _Path;
            set => _Path = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Path)));
        }

        private string _Hostname = "localhost";
        private int _Port = 9464;
        private string _Path = "/metrics";
    }
}
