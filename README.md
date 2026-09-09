<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/MinCMS/main/assets/logo.png" alt="MinCMS Logo" width="256">
</p>

<p align="center">
  <a href="https://github.com/jchristn/MinCMS/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10.0"></a>
  <a href="https://react.dev/"><img src="https://img.shields.io/badge/React-19-61DAFB?logo=react" alt="React 19"></a>
  <a href="https://docs.docker.com/compose/"><img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" alt="Docker Compose"></a>
</p>

# MinCMS

MinCMS is a minimal, self-hosted content management system backed by S3-compatible storage. It provides a Watson-based .NET API, a React dashboard, built-in OpenAPI/Swagger documentation, public download pages, and browser-safe CORS/preflight handling without requiring a traditional database.

## Highlights

- S3-backed collections and file metadata with no relational database
- Watson 7 webserver stack with key-based authentication
- Full metrics, tracing, and logs (OpenTelemetry / Prometheus / Grafana) — see [TELEMETRY.md](TELEMETRY.md)
- Built-in `/openapi.json` and `/swagger`
- Documented CORS preflight support for all non-download routes
- Public HTML download listings and download links
- Local Docker Compose build flow for the MinCMS server and dashboard

## Quick Start

### Prerequisites

- Docker Desktop or Docker Engine with Compose support
- An S3-compatible backend if you do not want to use the bundled Less3 container

### Start the stack

From the repository root:

```bash
cd docker
docker compose up --build -d
```

The first run builds `mincms-server` from `../src` and `mincms-dashboard` from `../dashboard`. The bundled Less3 services are still pulled as images because their source is not part of this repository.
The compose stack also bootstraps the Less3 runtime state on container startup; when `docker/less3/db` is empty, it copies the checked-in factory Less3 database into place before Less3 starts.

### Default URLs

| Service | URL |
|---|---|
| Less3 API | `http://localhost:8000` |
| Less3 UI | `http://localhost:3000` |
| MinCMS API | `http://localhost:8200` |
| Swagger UI | `http://localhost:8200/swagger` |
| OpenAPI JSON | `http://localhost:8200/openapi.json` |
| MinCMS Dashboard | `http://localhost:8300` |
| Grafana | `http://localhost:3001` (admin / admin) |
| Prometheus | `http://localhost:9090` |
| Tempo | `http://localhost:3200` |
| Loki | `http://localhost:3100` |

### Default API key

The checked-in Docker config seeds one access key:

```text
mincmsadmin
```

Change it before using the stack anywhere outside local development.

## Architecture

| Component | Technology | Purpose |
|---|---|---|
| `MinCms.Server` | .NET 10 + Watson 7 | Authenticated REST API, OpenAPI, Swagger, CORS, public downloads |
| `MinCms.Dashboard` | React 19 + nginx | Browser UI for collections and files |
| `Less3` | S3-compatible object storage | Local default backend for files and metadata |

All collection metadata and file content live in S3-compatible storage. MinCMS does not require a separate SQL or document database.

## API Surface

Authenticated API routes:

- `GET /v1.0/collections`
- `POST /v1.0/collections`
- `GET /v1.0/collections/{slug}`
- `DELETE /v1.0/collections/{slug}`
- `GET /v1.0/collections/{slug}/files`
- `POST /v1.0/collections/{slug}/files`
- `GET /v1.0/collections/{slug}/files/{fileName}`
- `DELETE /v1.0/collections/{slug}/files`
- `DELETE /v1.0/collections/{slug}/files/{fileName}`

Public routes:

- `HEAD /`
- `GET /`
- `GET /openapi.json`
- `GET /swagger`
- `GET /download/{slug}`
- `GET /download/{slug}/sitemap.xml`
- `GET /download/{slug}/{fileName}`

Every non-download route also accepts `OPTIONS` for browser preflight. Those preflight operations are included in the generated OpenAPI document. Dynamic download routes remain intentionally excluded from Swagger/OpenAPI.

Detailed route documentation lives in [REST_API.md](REST_API.md).

## Configuration

The server reads `mincms.json` on startup, then applies supported environment-variable overrides.

### Example `mincms.json`

```json
{
  "Rest": {
    "Hostname": "localhost",
    "Port": 8200,
    "Ssl": false
  },
  "S3": {
    "AccessKey": "",
    "SecretKey": "",
    "Bucket": "",
    "Region": "",
    "EndpointUrl": null,
    "UseSsl": true,
    "RequestStyle": "VirtualHosted"
  },
  "AccessKeys": [
    {
      "Name": "Admin",
      "Key": "mincmsadmin"
    }
  ],
  "Logging": {
    "ConsoleLogging": true,
    "MinimumSeverity": 1,
    "EnableColors": false,
    "FileLogging": true,
    "LogDirectory": "./logs/",
    "LogFilename": "mincms.log",
    "IncludeDateInFilename": true,
    "Servers": []
  },
  "Cors": {
    "AllowedOrigins": [
      "*"
    ],
    "AllowedMethods": [
      "GET",
      "HEAD",
      "OPTIONS",
      "POST",
      "PUT",
      "PATCH",
      "DELETE"
    ],
    "AllowedHeaders": [
      "*"
    ],
    "ExposeHeaders": [
      "Content-Disposition",
      "Content-Length",
      "Content-Type",
      "ETag"
    ],
    "MaxAgeSeconds": 86400
  }
}
```

### CORS behavior

- If `Cors` is omitted or explicitly `null`, MinCMS initializes it to the permissive defaults shown above.
- `AllowedOrigins: ["*"]` allows any origin.
- `AllowedHeaders: ["*"]` mirrors the browser-requested headers during preflight.
- Normal API responses also emit CORS headers, not only `OPTIONS` responses.
- Download routes are public, but the OpenAPI document only covers non-download routes.

### Supported server environment variables

| Variable | Overrides | Notes |
|---|---|---|
| `WEBSERVER_HOSTNAME` | `Rest.Hostname` | Listener hostname |
| `WEBSERVER_PORT` | `Rest.Port` | Listener port |
| `S3_ACCESS_KEY` | `S3.AccessKey` | S3 access key |
| `S3_SECRET_KEY` | `S3.SecretKey` | S3 secret key |
| `S3_BUCKET` | `S3.Bucket` | Bucket name |
| `S3_REGION` | `S3.Region` | Region |
| `S3_ENDPOINT` | `S3.EndpointUrl` | Custom S3 endpoint |
| `S3_USE_SSL` | `S3.UseSsl` | `true` or `false` |
| `S3_REQUEST_STYLE` | `S3.RequestStyle` | `VirtualHosted` or `PathStyle` |

Dashboard runtime config comes from these environment variables:

| Variable | Default |
|---|---|
| `MINCMS_SERVER_URL` | `http://localhost:8200` |
| `MINCMS_LOGO_FILE` | `/assets/logo.png` |
| `MINCMS_LOGO_NOTEXT_FILE` | `/assets/logo-no-text.png` |
| `MINCMS_FAVICON_FILE` | `/assets/logo-no-text.ico` |

## Docker Notes

- `docker/compose.yaml` builds the server and dashboard from local source.
- The MinCMS server container exposes `8200`.
- The dashboard container exposes `8300`.
- The reverse-proxy example in `docker/nginx/nginx.conf` now assumes `8200` for the API upstream.
- If you update source code and want a rebuild, run:

```bash
cd docker
docker compose up --build -d
```

### Resetting the local Docker deployment

From the `docker` directory:

- Windows: `factory/reset.bat`
- macOS/Linux: `bash factory/reset.sh`

That restores the checked-in factory config for the local Docker deployment, including the default Less3 database with the bundled `default` user, credential, and bucket, plus the MinCMS server/dashboard runtime config.

## Building From Source

### Server

```bash
dotnet build src/MinCms.sln
dotnet run --project src/MinCms.Server
```

### Dashboard

```bash
cd dashboard
npm install
npm run dev
```

### Local image helper scripts

The repository includes two convenience scripts that build local images without pushing anything to Docker Hub:

```bash
build-server.bat v0.1.0
build-dashboard.bat v0.1.0
```

They produce local tags:

- `mincms-server:latest`
- `mincms-server:<tag>`
- `mincms-dashboard:latest`
- `mincms-dashboard:<tag>`

## Development Notes

- NuGet versions are centrally managed in `src/Directory.Packages.props`.
- The generated OpenAPI document is served from `/openapi.json`.
- Swagger UI is served from `/swagger`.
- Dynamic download routes are intentionally excluded from the OpenAPI document because they are public and path-driven.

## Related Files

- [TELEMETRY.md](TELEMETRY.md): metrics, traces, logs, and observability-stack integration
- [REST_API.md](REST_API.md): route-by-route API reference
- [MinCMS.postman_collection.json](MinCMS.postman_collection.json): Postman collection
- [docker/compose.yaml](docker/compose.yaml): local deployment
- [docker/server/mincms.json](docker/server/mincms.json): default server configuration

## License

MinCMS is released under the [MIT License](LICENSE).
