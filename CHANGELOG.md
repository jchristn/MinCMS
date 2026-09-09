# Change Log

## Current Version v0.1.1

- Added end-to-end observability built on `System.Diagnostics.Metrics` / `System.Diagnostics` and the Radiant telemetry SDK (OpenTelemetry export)
- Metrics, traces, and structured logs for all HTTP routes, the collection application layer, the S3 storage layer, plus .NET runtime and process health
- New `Telemetry` configuration section in `mincms.json` with `TELEMETRY_*` / `OTEL_EXPORTER_OTLP_*` environment overrides; disabled cleanly when no collector is present
- Docker Compose observability stack: OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana (pre-provisioned datasources and MinCMS dashboards) with no port conflicts
- Dashboard "Observability" cards linking out to Grafana, Prometheus, Tempo, and Loki (name, credentials, and URL)
- New [TELEMETRY.md](TELEMETRY.md) describing the signals emitted and how to integrate a broader observability stack

## Previous Version v0.1.0

- Initial release of MinCMS
- REST API server for content management backed by S3-compatible storage
- React dashboard for managing collections and files
- API key authentication
- File upload, download, and metadata management
- Docker support with multi-platform builds (linux/amd64, linux/arm64/v8)

## Previous Versions

Notes from previous versions will be copied here.
