# MinCMS REST API Reference

MinCMS exposes a RESTful API for managing collections and files backed by S3-compatible storage. This document covers every endpoint, request/response format, authentication, and error handling.

## Table of Contents

- [Base URL](#base-url)
- [OpenAPI / Swagger](#openapi--swagger)
- [Authentication](#authentication)
- [Error Responses](#error-responses)
- [Endpoints](#endpoints)
  - [Health](#health)
  - [Collections](#collections)
  - [Files](#files)
  - [Public Downloads](#public-downloads)
- [Data Models](#data-models)
- [Quick Reference](#quick-reference)
- [Notes](#notes)

---

## Base URL

The API is served from the root of the configured host. The default Docker Compose configuration exposes the server on port **8100**.

```
http://localhost:8100
```

All managed endpoints are prefixed with `/v1.0`. Public download endpoints use the `/download` prefix.

---

## OpenAPI / Swagger

MinCMS ships with built-in OpenAPI support. When the server is running you can access:

| Resource       | URL                                     | Description                                |
|----------------|------------------------------------------|--------------------------------------------|
| OpenAPI spec   | `http://localhost:8100/openapi.json`     | OpenAPI 3.x JSON specification             |
| Swagger UI     | `http://localhost:8100/swagger`          | Interactive API explorer                   |

The specification includes all endpoints, request/response schemas, and two registered security schemes:

| Scheme   | Type        | Location | Description                                      |
|----------|-------------|----------|--------------------------------------------------|
| `ApiKey` | API Key     | Header   | API key provided in the `x-api-key` header       |
| `Bearer` | HTTP Bearer | Header   | API key provided as a Bearer token               |

Tags used in the spec: **Health**, **Collections**, **Files**, **Downloads**.

---

## Authentication

All `/v1.0/*` endpoints require authentication. Public download endpoints (`/download/*`) and the health check (`/`) do **not** require authentication.

Provide your API key using **one** of the following methods:

### API Key Header (recommended)

```
x-api-key: your-api-key
```

### Bearer Token

```
Authorization: Bearer your-api-key
```

The server checks `x-api-key` first, then falls back to `Authorization: Bearer`. Key comparison is case-sensitive.

Access keys are defined in `mincms.json`:

```json
{
  "AccessKeys": [
    { "Name": "Admin", "Key": "mincmsadmin" }
  ]
}
```

The default key is `mincmsadmin` — change it before deploying to production.

---

## Error Responses

All errors return a consistent JSON body:

```json
{
  "error": "ErrorCode",
  "statusCode": 400,
  "message": "Human-readable error message.",
  "context": null,
  "description": "Additional details or exception information."
}
```

### Error Codes

| Error Code              | HTTP Status | Message                                                                                  |
|-------------------------|-------------|------------------------------------------------------------------------------------------|
| `AuthenticationFailed`  | 401         | Your authentication material was not accepted.                                           |
| `BadRequest`            | 400         | We were unable to discern your request. Please check your URL, query, and request body.  |
| `NotFound`              | 404         | The requested resource was not found.                                                    |
| `Conflict`              | 409         | Operation failed as it would create a conflict with an existing resource.                |
| `InternalError`         | 500         | An internal error has been encountered.                                                  |
| `Timeout`               | 408         | The request was not completed within the specified timeout interval.                     |
| `TooLarge`              | 413         | The size of your request exceeds the maximum allowed by this server.                     |

### Exception-to-Status Mapping

| Exception Type                                               | Status | Error Code     |
|--------------------------------------------------------------|--------|----------------|
| `ArgumentException`, `ArgumentNullException`, `FormatException`, `JsonException` | 400    | `BadRequest`   |
| `FileNotFoundException`, `KeyNotFoundException`              | 404    | `NotFound`     |
| `InvalidOperationException`                                  | 409    | `Conflict`     |
| `TaskCanceledException`, `OperationCanceledException`        | 503    | `Timeout`      |
| All other exceptions                                         | 500    | `InternalError`|

---

## Endpoints

### Health

#### `HEAD /` — Health Check

Lightweight health probe. Returns an empty response.

| Detail          | Value             |
|-----------------|-------------------|
| Authentication  | None              |
| Tag             | Health            |

**Response**

| Status | Body   |
|--------|--------|
| `200`  | Empty  |

---

#### `GET /` — Root Page

Returns an HTML page with the MinCMS logo confirming the server is running.

| Detail          | Value             |
|-----------------|-------------------|
| Authentication  | None              |
| Tag             | Health            |

**Response**

| Status | Content-Type | Body                       |
|--------|-------------|----------------------------|
| `200`  | `text/html` | HTML page with status info |

---

### Collections

#### `GET /v1.0/collections` — List Collections

Returns all collections.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Collections                |

**Request**

```http
GET /v1.0/collections HTTP/1.1
x-api-key: your-api-key
```

**Response**

| Status | Content-Type       | Body                              |
|--------|--------------------|-----------------------------------|
| `200`  | `application/json` | Array of [Collection](#collection) objects |

```json
[
  {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "name": "Product Releases",
    "slug": "product-releases",
    "createdUtc": "2025-06-15T10:30:00Z",
    "isActive": true
  }
]
```

**Errors:** `401`

---

#### `POST /v1.0/collections` — Create Collection

Creates a new collection.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Collections                |

**Request**

```http
POST /v1.0/collections HTTP/1.1
x-api-key: your-api-key
Content-Type: application/json

{
  "name": "Product Releases",
  "slug": "product-releases"
}
```

| Field  | Type   | Required | Description                        |
|--------|--------|----------|------------------------------------|
| `name` | string | Yes      | Display name for the collection    |
| `slug` | string | Yes      | URL-friendly identifier (unique)   |

**Response**

| Status | Content-Type       | Body                             |
|--------|--------------------|----------------------------------|
| `201`  | `application/json` | Created [Collection](#collection) object |

The server auto-generates `id`, sets `createdUtc` to the current UTC time, and sets `isActive` to `true`.

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "Product Releases",
  "slug": "product-releases",
  "createdUtc": "2025-06-15T10:30:00Z",
  "isActive": true
}
```

**Errors:** `400` (missing/empty name or slug), `401`, `409` (slug already exists)

---

#### `GET /v1.0/collections/{slug}` — Get Collection

Returns a single collection by its slug.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Collections                |

**Request**

```http
GET /v1.0/collections/product-releases HTTP/1.1
x-api-key: your-api-key
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |

**Response**

| Status | Content-Type       | Body                             |
|--------|--------------------|----------------------------------|
| `200`  | `application/json` | [Collection](#collection) object |

**Errors:** `401`, `404` (slug not found)

---

#### `DELETE /v1.0/collections/{slug}` — Delete Collection

Deletes a collection **and all of its files** from S3.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Collections                |

**Request**

```http
DELETE /v1.0/collections/product-releases HTTP/1.1
x-api-key: your-api-key
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |

**Response**

| Status | Body   |
|--------|--------|
| `204`  | Empty  |

> **Warning:** This is a destructive operation. All files within the collection are permanently deleted from S3.

**Errors:** `401`, `404`, `409`

---

### Files

#### `GET /v1.0/collections/{slug}/files` — List Files

Returns all files in a collection.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Files                      |

**Request**

```http
GET /v1.0/collections/product-releases/files HTTP/1.1
x-api-key: your-api-key
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |

**Response**

| Status | Content-Type       | Body                                         |
|--------|--------------------|----------------------------------------------|
| `200`  | `application/json` | Array of [CollectionFile](#collectionfile) objects |

```json
[
  {
    "key": "product-releases/installer-v2.0.exe",
    "fileName": "installer-v2.0.exe",
    "size": 52428800,
    "lastModifiedUtc": "2025-07-01T14:22:00Z",
    "contentType": "application/octet-stream",
    "eTag": "\"d41d8cd98f00b204e9800998ecf8427e\""
  }
]
```

Returns an empty array if the collection exists but contains no files.

**Errors:** `401`, `404` (collection not found)

---

#### `POST /v1.0/collections/{slug}/files` — Upload File

Uploads a file to a collection using multipart form data.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Files                      |

**Request**

```http
POST /v1.0/collections/product-releases/files HTTP/1.1
x-api-key: your-api-key
Content-Type: multipart/form-data; boundary=----FormBoundary

------FormBoundary
Content-Disposition: form-data; name="file"; filename="installer-v2.0.exe"
Content-Type: application/octet-stream

<binary file data>
------FormBoundary--
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |
| `file`    | body | binary | Yes      | File (multipart form field) |

- The `Content-Type` header **must** include a `boundary` parameter.
- The filename is extracted from the `Content-Disposition` header and URL-decoded.
- If no `Content-Type` is provided for the part, it defaults to `application/octet-stream`.
- Binary data is handled safely without encoding corruption.

**Response**

| Status | Content-Type       | Body                                    |
|--------|--------------------|---------------------------------------- |
| `201`  | `application/json` | Created [CollectionFile](#collectionfile) object |

**Errors:** `400` (malformed multipart, missing boundary, no file), `401`, `404` (collection not found), `409` (file already exists), `413` (payload too large)

**cURL Example**

```bash
curl -X POST "http://localhost:8100/v1.0/collections/product-releases/files" \
  -H "x-api-key: mincmsadmin" \
  -F "file=@./installer-v2.0.exe"
```

---

#### `GET /v1.0/collections/{slug}/files/{fileName}` — Get File Metadata

Returns metadata for a specific file. Does **not** return the file content — use the [download endpoint](#get-downloadslufilename--download-file) for that.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Files                      |

**Request**

```http
GET /v1.0/collections/product-releases/files/installer-v2.0.exe HTTP/1.1
x-api-key: your-api-key
```

| Parameter  | In   | Type   | Required | Description                     |
|------------|------|--------|----------|---------------------------------|
| `slug`     | path | string | Yes      | Collection slug                 |
| `fileName` | path | string | Yes      | Filename (URL-encode if needed) |

**Response**

| Status | Content-Type       | Body                                    |
|--------|--------------------|---------------------------------------- |
| `200`  | `application/json` | [CollectionFile](#collectionfile) object |

**Errors:** `401`, `404` (collection or file not found)

---

#### `DELETE /v1.0/collections/{slug}/files/{fileName}` — Delete File

Permanently deletes a file from S3.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Files                      |

**Request**

```http
DELETE /v1.0/collections/product-releases/files/installer-v2.0.exe HTTP/1.1
x-api-key: your-api-key
```

| Parameter  | In   | Type   | Required | Description                     |
|------------|------|--------|----------|---------------------------------|
| `slug`     | path | string | Yes      | Collection slug                 |
| `fileName` | path | string | Yes      | Filename (URL-encode if needed) |

**Response**

| Status | Body   |
|--------|--------|
| `204`  | Empty  |

**Errors:** `401`, `404`, `409`

---

#### `DELETE /v1.0/collections/{slug}/files` — Delete Multiple Files

Permanently deletes multiple files from S3 in a single request. S3 silently ignores any filenames that do not exist.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | Required                   |
| Tag             | Files                      |

**Request**

```http
DELETE /v1.0/collections/product-releases/files HTTP/1.1
x-api-key: your-api-key
Content-Type: application/json

{
  "FileNames": [
    "installer-v1.0.exe",
    "installer-v2.0.exe",
    "docs/readme.txt"
  ]
}
```

| Parameter   | In   | Type     | Required | Description                       |
|-------------|------|----------|----------|-----------------------------------|
| `slug`      | path | string   | Yes      | Collection slug                   |
| `FileNames` | body | string[] | Yes      | List of filenames to delete       |

**cURL Example**

```bash
curl -X DELETE http://localhost:8100/v1.0/collections/product-releases/files \
  -H "x-api-key: your-api-key" \
  -H "Content-Type: application/json" \
  -d '{"FileNames":["installer-v1.0.exe","docs/readme.txt"]}'
```

**Response**

| Status | Body                                |
|--------|-------------------------------------|
| `200`  | `{ "DeletedCount": 2 }`            |

**Errors:** `400` (empty FileNames), `401`, `404` (collection not found)

---

### Public Downloads

These endpoints are **unauthenticated** and intended for public file distribution.

#### `GET /download/{slug}` — Browse Files

Returns an HTML page listing all files in the collection with clickable download links.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | None                       |
| Tag             | Downloads                  |

**Request**

```http
GET /download/product-releases HTTP/1.1
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |

**Response**

| Status | Content-Type | Body                                              |
|--------|-------------|---------------------------------------------------|
| `200`  | `text/html` | Styled HTML table with file names, sizes, and dates |

The page displays:
- File names as clickable download links
- Human-readable file sizes (B, KB, MB, GB)
- Last modified timestamps in UTC
- Collection name and file count in the footer

**Errors:** `404` (collection not found or inactive)

---

#### `GET /download/{slug}/sitemap.xml` — Sitemap

Returns a standard XML sitemap for SEO indexing of the collection's files.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | None                       |
| Tag             | Downloads                  |

**Request**

```http
GET /download/product-releases/sitemap.xml HTTP/1.1
```

| Parameter | In   | Type   | Required | Description          |
|-----------|------|--------|----------|----------------------|
| `slug`    | path | string | Yes      | Collection slug      |

**Response**

| Status | Content-Type      | Body                                   |
|--------|-------------------|----------------------------------------|
| `200`  | `application/xml` | Sitemap XML per sitemaps.org schema    |

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>/download/product-releases/installer-v2.0.exe</loc>
    <lastmod>2025-07-01</lastmod>
  </url>
</urlset>
```

**Errors:** `404`

---

#### `GET /download/{slug}/{fileName}` — Download File

Streams a file directly from S3 as a browser download.

| Detail          | Value                      |
|-----------------|----------------------------|
| Authentication  | None                       |
| Tag             | Downloads                  |

**Request**

```http
GET /download/product-releases/installer-v2.0.exe HTTP/1.1
```

| Parameter  | In   | Type   | Required | Description                     |
|------------|------|--------|----------|---------------------------------|
| `slug`     | path | string | Yes      | Collection slug                 |
| `fileName` | path | string | Yes      | Filename (URL-encode if needed) |

**Response**

| Status | Headers                                                       | Body                |
|--------|---------------------------------------------------------------|---------------------|
| `200`  | `Content-Type`, `Content-Length`, `Content-Disposition`       | Binary file stream  |

Response headers:

| Header                | Example Value                                |
|-----------------------|----------------------------------------------|
| `Content-Type`        | `application/octet-stream`                   |
| `Content-Length`      | `52428800`                                   |
| `Content-Disposition` | `attachment; filename="installer-v2.0.exe"`  |

The file is streamed directly from S3 — the server does not buffer the entire file in memory.

**Errors:** `404` (collection or file not found)

---

## Data Models

### Collection

Represents a named group of files.

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "name": "Product Releases",
  "slug": "product-releases",
  "createdUtc": "2025-06-15T10:30:00Z",
  "isActive": true
}
```

| Field        | Type     | Description                                 |
|--------------|----------|---------------------------------------------|
| `id`         | string   | UUID, auto-generated on creation            |
| `name`       | string   | Display name                                |
| `slug`       | string   | URL-friendly identifier, unique across the instance |
| `createdUtc` | datetime | ISO 8601 UTC timestamp                      |
| `isActive`   | boolean  | Whether the collection is active            |

### CollectionFile

Represents a file stored within a collection.

```json
{
  "key": "product-releases/installer-v2.0.exe",
  "fileName": "installer-v2.0.exe",
  "size": 52428800,
  "lastModifiedUtc": "2025-07-01T14:22:00Z",
  "contentType": "application/octet-stream",
  "eTag": "\"d41d8cd98f00b204e9800998ecf8427e\""
}
```

| Field             | Type     | Description                           |
|-------------------|----------|---------------------------------------|
| `key`             | string   | Full S3 object key                    |
| `fileName`        | string   | Filename without the collection prefix|
| `size`            | integer  | File size in bytes                    |
| `lastModifiedUtc` | datetime | ISO 8601 UTC timestamp from S3       |
| `contentType`     | string   | MIME type                             |
| `eTag`            | string   | S3 ETag (useful for caching)         |

### DeleteFilesRequest

Request body for the batch file deletion endpoint.

```json
{
  "FileNames": [
    "installer-v1.0.exe",
    "installer-v2.0.exe",
    "docs/readme.txt"
  ]
}
```

| Field       | Type     | Description                          |
|-------------|----------|--------------------------------------|
| `FileNames` | string[] | List of filenames to delete          |

### DeleteFilesResponse

Response body for the batch file deletion endpoint.

```json
{
  "DeletedCount": 3
}
```

| Field          | Type    | Description                        |
|----------------|---------|------------------------------------|
| `DeletedCount` | integer | Number of files deleted            |

### ApiErrorResponse

Returned for all error responses.

```json
{
  "error": "NotFound",
  "statusCode": 404,
  "message": "The requested resource was not found.",
  "context": null,
  "description": "Collection 'nonexistent' does not exist."
}
```

| Field         | Type    | Description                                   |
|---------------|---------|-----------------------------------------------|
| `error`       | string  | Error code (see [Error Codes](#error-codes))  |
| `statusCode`  | integer | HTTP status code                              |
| `message`     | string  | Human-readable error message                  |
| `context`     | object  | Additional context (nullable)                 |
| `description` | string  | Detailed error information                    |

---

## Quick Reference

| Method   | Endpoint                                        | Auth | Status | Description             |
|----------|-------------------------------------------------|------|--------|-------------------------|
| `HEAD`   | `/`                                             | No   | `200`  | Health check            |
| `GET`    | `/`                                             | No   | `200`  | Root HTML page          |
| `GET`    | `/v1.0/collections`                             | Yes  | `200`  | List collections        |
| `POST`   | `/v1.0/collections`                             | Yes  | `201`  | Create collection       |
| `GET`    | `/v1.0/collections/{slug}`                      | Yes  | `200`  | Get collection          |
| `DELETE` | `/v1.0/collections/{slug}`                      | Yes  | `204`  | Delete collection       |
| `GET`    | `/v1.0/collections/{slug}/files`                | Yes  | `200`  | List files              |
| `POST`   | `/v1.0/collections/{slug}/files`                | Yes  | `201`  | Upload file             |
| `GET`    | `/v1.0/collections/{slug}/files/{fileName}`     | Yes  | `200`  | Get file metadata       |
| `DELETE` | `/v1.0/collections/{slug}/files/{fileName}`     | Yes  | `204`  | Delete file             |
| `DELETE` | `/v1.0/collections/{slug}/files`                | Yes  | `200`  | Delete multiple files   |
| `GET`    | `/download/{slug}`                              | No   | `200`  | Browse files (HTML)     |
| `GET`    | `/download/{slug}/sitemap.xml`                  | No   | `200`  | Sitemap (XML)           |
| `GET`    | `/download/{slug}/{fileName}`                   | No   | `200`  | Download file           |
| `GET`    | `/openapi.json`                                 | No   | `200`  | OpenAPI specification   |
| `GET`    | `/swagger`                                      | No   | `200`  | Swagger UI              |

---

## Notes

### URL Encoding

Filenames containing special characters (spaces, unicode, etc.) must be percent-encoded in the URL path. The server automatically decodes them before processing. For example, `my file.pdf` should be requested as `my%20file.pdf`.

### Pagination

The API does not implement pagination. All list endpoints return the complete result set. MinCMS is designed for use cases with a manageable number of collections and files per collection.

### Concurrency

Collection metadata mutations are serialized using a semaphore to prevent race conditions. S3 ETag-based optimistic concurrency control is used for safe concurrent writes to the configuration file stored in S3.

### Content Types

- All JSON endpoints return `application/json`.
- The `Content-Type` header is set automatically on responses by the server.
- File uploads must use `multipart/form-data` with a valid `boundary`.
- Downloaded files carry the content type that was detected at upload time.
