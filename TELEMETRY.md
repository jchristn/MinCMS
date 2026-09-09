# MinCMS Telemetry

MinCMS is fully instrumented for **metrics, distributed traces, and structured logs**. Instrumentation is emitted through the .NET base class library (`System.Diagnostics.Metrics.Meter`, `System.Diagnostics.ActivitySource` / `Activity`) and exported through the [Radiant](https://www.nuget.org/packages/Radiant) telemetry SDK, which wires an OpenTelemetry pipeline (OTLP export, optional in-process Prometheus scrape, .NET runtime + process metrics).

Because emit rides the BCL, **the instruments stay a no-op until a telemetry host subscribes.** When telemetry is disabled (or no collector is reachable), MinCMS runs exactly as before with negligible overhead.

- [What is emitted](#what-is-emitted)
- [Configuration](#configuration)
- [Running the bundled observability stack](#running-the-bundled-observability-stack)
- [Connecting your own observability stack](#connecting-your-own-observability-stack)
- [Prometheus metric names](#prometheus-metric-names)
- [Notes for a DevOps / SRE integration](#notes-for-a-devops--sre-integration)

---

## What is emitted

### Coverage

| Path | Metrics | Traces | Notes |
|---|---|---|---|
| HTTP (all routes) | ✅ | ✅ | Every Watson route: API, downloads, docs, health, CORS preflight, unmatched |
| Collection application layer | ✅ | ✅ | Every `CollectionService` operation |
| S3 storage layer | ✅ | ✅ | Every `S3Service` operation + bytes read/written |
| .NET runtime | ✅ | — | GC, heap, threads, JIT, exceptions (OpenTelemetry runtime instrumentation) |
| Process | ✅ | — | Working set, uptime, thread count |
| Logs | — | — | Application logs flow through the existing SyslogLogging pipeline; the telemetry host also exposes an OTLP log pipeline |

### Meters and activity sources

A telemetry host subscribes to these names. If you host telemetry yourself (rather than using the built-in bootstrap), subscribe to all of them.

| Name | Kind | Signals |
|---|---|---|
| `MinCMS` | Meter + ActivitySource | Service-named meter: runtime + process metrics |
| `MinCms.Http` | Meter + ActivitySource | Inbound HTTP metrics and server spans |
| `MinCms.Collections` | Meter + ActivitySource | Collection operation metrics and spans |
| `MinCms.Storage` | Meter + ActivitySource | S3 operation metrics and spans |

### Metrics (OpenTelemetry instrument names)

**HTTP** (`MinCms.Http`) — names/tags follow the OpenTelemetry HTTP semantic conventions:

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `http.server.request.duration` | Histogram | s | `http.request.method`, `http.route`, `http.response.status_code`, `url.scheme` |
| `http.server.active_requests` | UpDownCounter | {request} | `http.request.method`, `url.scheme` |
| `http.server.request.errors` | Counter | {request} | `http.request.method`, `http.route`, `http.response.status_code` |
| `mincms.http.auth.failures` | Counter | {request} | `http.route` |

`http.route` is always a **low-cardinality route template** (e.g. `/v1.0/collections/{slug}/files/{fileName}`), never a raw path.

**Collections** (`MinCms.Collections`):

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `mincms.collection.operations` | Counter | {operation} | `operation`, `outcome` |
| `mincms.collection.operation.errors` | Counter | {operation} | `operation` |
| `mincms.collection.operation.duration` | Histogram | s | `operation`, `outcome` |
| `mincms.collection.operations.inflight` | UpDownCounter | {operation} | `operation` |

`operation` values: `list_collections`, `get_collection`, `create_collection`, `delete_collection`, `list_files`, `upload_file`, `download_file`, `delete_file`, `batch_delete_files`, `get_file_metadata`.

**Storage** (`MinCms.Storage`):

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `mincms.storage.operations` | Counter | {operation} | `operation`, `outcome` |
| `mincms.storage.operation.errors` | Counter | {operation} | `operation` |
| `mincms.storage.operation.duration` | Histogram | s | `operation`, `outcome` |
| `mincms.storage.operations.inflight` | UpDownCounter | {operation} | `operation` |
| `mincms.storage.bytes.read` | Counter | By | — |
| `mincms.storage.bytes.written` | Counter | By | — |

`operation` values: `load_collections`, `save_collections`, `get_collections_etag`, `list_objects`, `put_object`, `get_object`, `delete_object`, `batch_delete_objects`, `delete_prefix`, `head_object`, `head_object_exists`, `ensure_config`.

**Runtime & process** (`MinCMS`): `process.memory.usage` (By), `process.uptime` (s), `process.thread.count` ({thread}), plus the standard `process.runtime.dotnet.*` series (GC, heap, threadpool, exceptions) from OpenTelemetry runtime instrumentation.

### Traces

Each HTTP request opens a **server span** (`MinCms.Http`) named `<METHOD> <route>`. Application work started inside the request nests underneath it, so a single trace looks like:

```
GET /v1.0/collections/{slug}/files            (server span, MinCms.Http)
└─ collections.list_files                      (internal span, MinCms.Collections)
   ├─ collections.get_collection               (internal span, MinCms.Collections)
   │  └─ storage.load_collections              (internal span, MinCms.Storage)
   └─ storage.list_objects                     (internal span, MinCms.Storage)
```

Spans carry `http.request.method`, `http.route`, `http.response.status_code`, `url.scheme`, `mincms.domain`, and `operation` tags. Exceptions are recorded as standard `exception` span events (`exception.type`, `exception.message`, `exception.stacktrace`) and set the span status to error. Sampling is head-based and parent-based (`TraceSamplingRatio`, default `1.0`).

---

## Configuration

Telemetry is configured in the `Telemetry` section of `mincms.json` and can be overridden by environment variables (env wins).

```json
"Telemetry": {
  "Enable": true,
  "ServiceName": "MinCMS",
  "ServiceInstanceId": null,
  "Otlp": {
    "Enable": true,
    "Endpoint": "http://localhost:4317",
    "Protocol": "Grpc",
    "TimeoutMs": 10000
  },
  "Prometheus": {
    "Enable": false,
    "Hostname": "localhost",
    "Port": 9464,
    "Path": "/metrics"
  },
  "MetricsExportIntervalMs": 15000,
  "IncludeRuntimeMetrics": true,
  "IncludeProcessMetrics": true,
  "TraceSamplingRatio": 1.0
}
```

### Environment variable overrides

| Variable | Overrides | Notes |
|---|---|---|
| `TELEMETRY_ENABLED` | `Telemetry.Enable` | `true` / `false` master switch |
| `TELEMETRY_SERVICE_NAME` | `Telemetry.ServiceName` | `service.name` resource attribute |
| `OTEL_EXPORTER_OTLP_ENABLED` | `Telemetry.Otlp.Enable` | Enable/disable OTLP push |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `Telemetry.Otlp.Endpoint` | e.g. `http://otel-collector:4317` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `Telemetry.Otlp.Protocol` | `grpc` (4317) or `http/protobuf` (4318) |
| `TELEMETRY_PROMETHEUS_ENABLED` | `Telemetry.Prometheus.Enable` | Enable in-process scrape endpoint |
| `TELEMETRY_PROMETHEUS_PORT` | `Telemetry.Prometheus.Port` | Default 9464 |

### Two export modes

- **Push (OTLP, default).** MinCMS pushes metrics, traces, and logs to an OpenTelemetry Collector at `Otlp.Endpoint`. This is the recommended path and what the bundled stack uses.
- **Pull (in-process Prometheus).** Set `Prometheus.Enable = true` to have MinCMS serve `/metrics` on `Prometheus.Port` (default `9464`) for a Prometheus server to scrape directly — useful when you don't run a collector. Metrics only; traces and logs still require OTLP.

Both can run simultaneously. If OTLP is enabled but no collector is reachable, the exporter retries quietly and the server keeps serving traffic; telemetry never blocks a request.

---

## Running the bundled observability stack

`docker/compose.yaml` includes a complete stack. Bring everything up from the `docker` directory:

```bash
cd docker
docker compose up -d
```

> The `mincms-server` and `mincms-dashboard` images are pulled published tags. To exercise the new telemetry from source, rebuild those images from `../src` and `../dashboard` first (see the build scripts in the repo root), then `docker compose up -d`.

### Services, ports, and credentials

| Service | URL | Default credentials | Purpose |
|---|---|---|---|
| Grafana | http://localhost:3001 | `admin` / `admin` | Dashboards & visualization |
| Prometheus | http://localhost:9090 | none | Metrics store |
| Tempo | http://localhost:3200 | none | Trace store |
| Loki | http://localhost:3100 | none | Log store |
| OTel Collector | grpc `localhost:4317`, http `localhost:4318` | none | Ingest / fan-out |

Grafana runs on **3001** (host) because `3000` is already used by the Less3 UI. These same links are surfaced as **Observability cards** on the MinCMS dashboard home page (name, credentials, and URL each), driven by the `MINCMS_*_URL` / `MINCMS_*_CREDENTIALS` environment variables on the `mincms-dashboard` container.

### Grafana content

Datasources (Prometheus, Tempo, Loki) are pre-provisioned with metric↔trace↔log correlation (exemplars, trace-to-logs, log-derived trace IDs). Dashboards are provisioned into a single top-level **`MinCMS`** folder, one per domain:

- **MinCMS - HTTP** — request rate, latency percentiles, in-flight, 5xx, auth failures
- **MinCMS - Collections** — operation rate, outcome, duration, errors, in-flight
- **MinCMS - Storage** — S3 op rate, errors, duration, throughput (bytes), in-flight
- **MinCMS - Runtime** — working set, GC heap/collections, exceptions, threads, uptime

---

## Connecting your own observability stack

MinCMS speaks standard OTLP, so it drops into any OpenTelemetry-compatible backend (Grafana stack, Honeycomb, Datadog, New Relic, Elastic, Splunk, Dynatrace, an existing collector, etc.).

1. **Point OTLP at your collector / endpoint:**
   ```bash
   OTEL_EXPORTER_OTLP_ENDPOINT=https://otel.your-domain.example:4317
   OTEL_EXPORTER_OTLP_PROTOCOL=grpc     # or http/protobuf for :4318
   ```
   For a hosted backend that needs an auth header, add it to `Telemetry.Otlp.Headers` in `mincms.json` (e.g. an API key), or route through your own collector that attaches credentials.

2. **Or scrape MinCMS directly** if you already run Prometheus and don't want a collector:
   ```bash
   TELEMETRY_PROMETHEUS_ENABLED=true
   TELEMETRY_PROMETHEUS_PORT=9464
   ```
   Then add a scrape job targeting `mincms-host:9464/metrics`.

3. **Resource attributes.** Every signal carries `service.name` (default `MinCMS`) and `service.instance.id`. Set `TELEMETRY_SERVICE_NAME` per environment/tenant so multiple MinCMS deployments stay distinguishable in your backend.

---

## Prometheus metric names

When metrics flow through the OpenTelemetry Collector's Prometheus exporter, the dotted OTel names are translated to Prometheus conventions: dots become underscores, counters gain `_total`, and units are suffixed. Examples:

| OpenTelemetry name | Prometheus series |
|---|---|
| `http.server.request.duration` (histogram, s) | `http_server_request_duration_seconds_bucket` / `_sum` / `_count` |
| `http.server.active_requests` | `http_server_active_requests` |
| `http.server.request.errors` | `http_server_request_errors_total` |
| `mincms.collection.operations` | `mincms_collection_operations_total` |
| `mincms.storage.bytes.written` (By) | `mincms_storage_bytes_written_bytes_total` |
| `process.memory.usage` (By) | `process_memory_usage_bytes` |

The provisioned Grafana dashboards already use these translated names. If your collector or Prometheus applies different normalization, adjust the dashboard queries accordingly.

---

## Notes for a DevOps / SRE integration

- **Cardinality is bounded by design.** Metric labels are deliberately low-cardinality: `operation`, `outcome`, `http.route` (template only), method, status, scheme. Per-request identifiers (slugs, filenames, IPs) are **never** put on metrics — they live on spans/logs. You can safely aggregate without a time-series explosion.
- **Sampling.** Traces default to 100% (`TraceSamplingRatio = 1.0`). Lower it (e.g. `0.1`) in high-traffic production; sampling is parent-based so a sampled request keeps its whole trace.
- **Overhead & failure isolation.** Instruments are no-ops when nothing subscribes. If the collector is down, the OTLP exporter buffers/retries and drops on timeout without affecting request latency. Telemetry startup failures are logged and swallowed — the server still starts.
- **Correlation.** Enable trace/log correlation in your backend using the W3C trace context propagated by MinCMS. The bundled Grafana wiring demonstrates exemplars (metric→trace) and derived fields (log→trace).
- **Retention/storage.** The bundled Tempo/Loki/Prometheus use local volumes with modest retention (suitable for local/dev). For production, point OTLP at your managed backend or replace the bundled stores with your retention policy.
- **Scaling the collector.** For multiple MinCMS instances, run a shared/gateway collector and set `OTEL_EXPORTER_OTLP_ENDPOINT` on each instance; distinguish them via `service.instance.id` (auto-generated) or a per-instance `TELEMETRY_SERVICE_NAME`.
- **Ports to open.** OTLP gRPC `4317` (or HTTP `4318`) from MinCMS to the collector; optionally `9464` for direct Prometheus scrape. Grafana `3001`, Prometheus `9090`, Tempo `3200`, Loki `3100` in the bundled stack.
