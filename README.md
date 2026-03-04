<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/MinCMS/main/assets/logo.png" alt="MinCMS Logo" width="256">
</p>

<p align="center">
  <a href="https://github.com/jchristn/MinCMS/blob/main/LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT License"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10.0"></a>
  <a href="https://react.dev/"><img src="https://img.shields.io/badge/React-19-61DAFB?logo=react" alt="React 19"></a>
  <a href="https://docs.docker.com/compose/"><img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" alt="Docker Compose"></a>
  <a href="https://hub.docker.com/r/jchristn77/mincms-server"><img src="https://img.shields.io/docker/pulls/jchristn77/mincms-server?label=Server%20Pulls&logo=docker" alt="Docker Pulls"></a>
  <a href="https://hub.docker.com/r/jchristn77/mincms-dashboard"><img src="https://img.shields.io/docker/pulls/jchristn77/mincms-dashboard?label=Dashboard%20Pulls&logo=docker" alt="Docker Pulls"></a>
</p>

# MinCMS

**MinCMS** is a minimal, self-hosted content management system backed by S3-compatible storage. Upload, organize, and share files through a clean REST API and a modern React dashboard — no traditional database required.

MinCMS stores everything (files and metadata) in any S3-compatible bucket — AWS S3, Less3, MinIO, Wasabi, Backblaze B2, and more — so you keep full control of your data.

## Why MinCMS?

- **Simple by design** — no database, no complex setup, just S3 and go
- **S3-compatible** — works with AWS S3, MinIO, Wasabi, Backblaze B2, and any S3-compatible provider
- **Public download pages** — share collections of files via clean, browsable URLs
- **API-first** — every operation is available through a straightforward REST API
- **Modern dashboard** — manage collections and files through a responsive React UI with light/dark mode
- **Docker-ready** — up and running in minutes with Docker Compose
- **Multi-platform** — images available for linux/amd64 and linux/arm64

## Use Cases

- **File distribution** — share downloads, release artifacts, or media assets with public-facing download pages
- **Content management** — organize files into collections for different projects, teams, or clients
- **Digital asset management** — centralize images, documents, and media with metadata tracking
- **Self-hosted file sharing** — a lightweight alternative to heavier CMS platforms when all you need is file management
- **Headless CMS backend** — use the REST API to serve content to websites, apps, or CI/CD pipelines

## Getting Started

### Prerequisites

- [Docker](https://docs.docker.com/get-docker/) and [Docker Compose](https://docs.docker.com/compose/install/)
- An S3-compatible storage account (AWS S3, MinIO, Wasabi, etc.)

### Quick Start with Docker Compose

1. **Clone the repository**

   ```bash
   git clone https://github.com/jchristn/MinCMS.git
   cd MinCMS/docker
   ```

2. **Configure your S3 credentials (required before starting)**

   Edit `docker/server/mincms.json` and fill in your S3 settings. The server will fail to start if these are not configured.

   **AWS S3:**

   ```json
   {
     "S3": {
       "AccessKey": "your-access-key",
       "SecretKey": "your-secret-key",
       "Bucket": "your-bucket-name",
       "Region": "us-east-1"
     }
   }
   ```

   **MinIO or other S3-compatible services** — also set `EndpointUrl` and `RequestStyle`:

   ```json
   {
     "S3": {
       "AccessKey": "minioadmin",
       "SecretKey": "minioadmin",
       "Bucket": "my-bucket",
       "Region": "us-east-1",
       "EndpointUrl": "http://localhost:9000",
       "UseSsl": false,
       "RequestStyle": "PathStyle"
     }
   }
   ```

   > **Important:** You must update `docker/server/mincms.json` with valid S3 credentials **before** running `docker compose up`. The configuration file is mounted into the container at startup — changes made after the container is running require a restart (`docker compose restart mincms-server`).

3. **Start the stack**

   ```bash
   docker compose up -d
   ```

4. **Access the services**

   | Service   | URL                        |
   |-----------|----------------------------|
   | API       | http://localhost:8200      |
   | Dashboard | http://localhost:8300      |

5. **Log into the dashboard** using the default API key: `mincmsadmin`

> **Tip:** Change the default access key in `mincms.json` before deploying to production.

## Architecture

MinCMS consists of two services:

| Component              | Technology    | Description                               |
|------------------------|---------------|-------------------------------------------|
| **MinCms.Server**      | .NET 10       | REST API for managing collections & files |
| **MinCms.Dashboard**   | React 19      | Web UI for browsing and managing content  |

All data — both file content and collection metadata — is stored in your S3 bucket. There is no separate database to manage, back up, or migrate.

```
┌──────────────┐      ┌──────────────┐      ┌──────────────────┐
│   Dashboard  │────> │  API Server  │────> │  S3-Compatible   │
│  (React 19)  │      │  (.NET 10)   │      │     Storage      │
└──────────────┘      └──────────────┘      └──────────────────┘
     :8300                :8200              AWS / Less3 / MinIO
```

## API Overview

All API endpoints (except health checks and public downloads) require authentication via `x-api-key` header or `Authorization: Bearer <key>`.

### Collections

| Method   | Endpoint                      | Description                |
|----------|-------------------------------|----------------------------|
| `GET`    | `/v1.0/collections`           | List all collections       |
| `POST`   | `/v1.0/collections`           | Create a new collection    |
| `GET`    | `/v1.0/collections/{slug}`    | Get collection details     |
| `DELETE` | `/v1.0/collections/{slug}`    | Delete collection & files  |

### Files

| Method   | Endpoint                                      | Description          |
|----------|-----------------------------------------------|----------------------|
| `GET`    | `/v1.0/collections/{slug}/files`              | List files           |
| `POST`   | `/v1.0/collections/{slug}/files`              | Upload a file        |
| `GET`    | `/v1.0/collections/{slug}/files/{fileName}`   | Get file metadata    |
| `DELETE` | `/v1.0/collections/{slug}/files/{fileName}`   | Delete a file        |
| `DELETE` | `/v1.0/collections/{slug}/files`              | Delete multiple files|

### Public Downloads (No Authentication)

| Method   | Endpoint                            | Description                |
|----------|-------------------------------------|----------------------------|
| `GET`    | `/download/{slug}`                  | Browsable file listing     |
| `GET`    | `/download/{slug}/{fileName}`       | Download a file            |
| `GET`    | `/download/{slug}/sitemap.xml`      | XML sitemap for SEO        |

## Configuration

MinCMS is configured through `mincms.json` and environment variables. Environment variables take precedence, making it easy to override settings per deployment.

### Settings File (mincms.json)

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
  }
}
```

### Server Environment Variables

| Variable             | Overrides           | Description                          |
|----------------------|---------------------|--------------------------------------|
| `WEBSERVER_HOSTNAME` | `Rest.Hostname`     | Webserver listen hostname            |
| `WEBSERVER_PORT`     | `Rest.Port`         | Webserver listen port                |
| `S3_ACCESS_KEY`      | `S3.AccessKey`      | S3 access key                        |
| `S3_SECRET_KEY`      | `S3.SecretKey`      | S3 secret key                        |
| `S3_BUCKET`          | `S3.Bucket`         | S3 bucket name                       |
| `S3_REGION`          | `S3.Region`         | AWS region                           |
| `S3_ENDPOINT`        | `S3.EndpointUrl`    | Custom S3-compatible endpoint URL    |
| `S3_USE_SSL`         | `S3.UseSsl`         | Use SSL for S3 (`true`/`false`)      |
| `S3_REQUEST_STYLE`   | `S3.RequestStyle`   | `VirtualHosted` or `PathStyle`       |

### Dashboard Environment Variables

| Variable                  | Default                      | Description                   |
|---------------------------|------------------------------|-------------------------------|
| `MINCMS_SERVER_URL`       | `http://localhost:8200`      | Server URL for the login page |
| `MINCMS_LOGO_FILE`        | `/assets/logo.png`           | Login page logo               |
| `MINCMS_LOGO_NOTEXT_FILE` | `/assets/logo-no-text.png`   | Top bar logo                  |
| `MINCMS_FAVICON_FILE`     | `/assets/logo-no-text.ico`   | Browser favicon               |

### Configuration Precedence

```
Environment variable (if set and non-empty)
  └─ overrides → mincms.json value
                   └─ overrides → built-in default
```

## Building from Source

### Server

```bash
cd src/MinCms.Server
dotnet build
dotnet run
```

### Dashboard

```bash
cd dashboard
npm install
npm run dev
```

### Docker Images

Multi-platform images (linux/amd64 and linux/arm64) can be built with the provided scripts:

```bash
build-server.bat v0.1.0
build-dashboard.bat v0.1.0
```

## Contributing

Contributions are welcome! Here's how to get involved:

- **Report bugs** — open an issue on the [Issues](https://github.com/jchristn/MinCMS/issues) tab
- **Request features** — open an issue describing your idea
- **Ask questions** — start a thread in the [Discussions](https://github.com/jchristn/MinCMS/discussions) tab
- **Submit a PR** — fork the repo, make your changes, and open a pull request

## License

MinCMS is released under the [MIT License](LICENSE). You are free to use, modify, and distribute it.

## Version History

See [CHANGELOG.md](CHANGELOG.md) for release notes.
