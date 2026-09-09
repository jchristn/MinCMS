namespace MinCms.Server.Telemetry
{
    using System;
    using MinCms.Core.Settings;
    using MinCms.Core.Telemetry;
    using Radiant;
    using SyslogLogging;

    /// <summary>
    /// Translates MinCMS <see cref="TelemetrySettings"/> into a Radiant telemetry host, subscribing to
    /// the MinCMS meter and activity-source names so HTTP, collection, storage, runtime, and process
    /// telemetry are all collected and exported.
    /// </summary>
    public static class TelemetryBootstrap
    {
        private const string _Header = "[Telemetry] ";

        /// <summary>
        /// Build and start a Radiant host from the supplied telemetry settings. Returns null when
        /// telemetry could not be started (the caller should continue running without it).
        /// </summary>
        /// <param name="settings">The MinCMS telemetry settings.</param>
        /// <param name="logging">The logging module used for diagnostics.</param>
        /// <returns>A started <see cref="RadiantHost"/>, or null on failure.</returns>
        public static RadiantHost Start(TelemetrySettings settings, LoggingModule logging)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (logging == null) throw new ArgumentNullException(nameof(logging));

            try
            {
                RadiantSettings radiant = new RadiantSettings(settings.ServiceName)
                {
                    Enable = settings.Enable,
                    ServiceInstanceId = String.IsNullOrEmpty(settings.ServiceInstanceId) ? null : settings.ServiceInstanceId,
                    DiagnosticCallback = message => logging.Warn(_Header + message)
                };

                // Subscribe to every MinCMS emit source: HTTP, collections, storage. The host's own
                // service-named meter/source (runtime + process metrics) is always subscribed.
                radiant.Sources
                    .AddMeter(HttpTelemetry.MeterName)
                    .AddMeter(CollectionTelemetry.MeterName)
                    .AddMeter(StorageTelemetry.MeterName);

                radiant.Sources
                    .AddActivitySource(HttpTelemetry.ActivitySourceName)
                    .AddActivitySource(CollectionTelemetry.ActivitySourceName)
                    .AddActivitySource(StorageTelemetry.ActivitySourceName);

                radiant.Otlp.Enable = settings.Otlp.Enable;
                radiant.Otlp.Endpoint = settings.Otlp.Endpoint;
                radiant.Otlp.Protocol = ParseProtocol(settings.Otlp.Protocol);
                radiant.Otlp.TimeoutMs = settings.Otlp.TimeoutMs;

                radiant.Prometheus.Enable = settings.Prometheus.Enable;
                radiant.Prometheus.Hostname = settings.Prometheus.Hostname;
                radiant.Prometheus.Port = settings.Prometheus.Port;
                radiant.Prometheus.Path = settings.Prometheus.Path;

                radiant.Metrics.ExportIntervalMs = settings.MetricsExportIntervalMs;
                radiant.Metrics.IncludeRuntime = settings.IncludeRuntimeMetrics;
                radiant.Metrics.IncludeProcess = settings.IncludeProcessMetrics;

                radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;

                RadiantHost host = RadiantHost.Start(radiant);

                if (host.IsEnabled)
                {
                    logging.Info(_Header + "telemetry enabled for service '" + settings.ServiceName
                        + "' (instance " + host.ServiceInstanceId + ")"
                        + (settings.Otlp.Enable ? " | OTLP -> " + settings.Otlp.Endpoint : " | OTLP disabled")
                        + (settings.Prometheus.Enable ? " | Prometheus " + settings.Prometheus.Hostname + ":" + settings.Prometheus.Port + settings.Prometheus.Path : ""));
                }
                else
                {
                    logging.Info(_Header + "telemetry disabled by configuration");
                }

                return host;
            }
            catch (Exception e)
            {
                logging.Warn(_Header + "failed to start telemetry; continuing without it:" + Environment.NewLine + e);
                return null;
            }
        }

        private static OtlpProtocolEnum ParseProtocol(string protocol)
        {
            if (String.IsNullOrEmpty(protocol)) return OtlpProtocolEnum.Grpc;

            // Accept both Radiant enum names ("Grpc", "HttpProtobuf") and the OTLP standard
            // environment values ("grpc", "http/protobuf").
            string normalized = protocol.Replace("/", "").Replace("_", "").Trim();

            if (normalized.Equals("grpc", StringComparison.OrdinalIgnoreCase)) return OtlpProtocolEnum.Grpc;
            if (normalized.Equals("httpprotobuf", StringComparison.OrdinalIgnoreCase)) return OtlpProtocolEnum.HttpProtobuf;
            if (Enum.TryParse(normalized, true, out OtlpProtocolEnum parsed)) return parsed;

            return OtlpProtocolEnum.Grpc;
        }
    }
}
