# MinCMS REST API Reference

This document describes the MinCMS HTTP interface as it exists in this repository after the Watson 7 migration.

## Base URLs

Default local endpoints:

- API root: `http://localhost:8200`
- OpenAPI JSON: `http://localhost:8200/openapi.json`
- Swagger UI: `http://localhost:8200/swagger`
- Dashboard: `http://localhost:8300`

All managed API routes are rooted at `/v1.0`. Public download routes are rooted at `/download`.

## OpenAPI And Swagger

MinCMS generates OpenAPI metadata at runtime.

Included in the generated spec:

- `GET` and `OPTIONS` on `/`
- `GET` and `OPTIONS` on `/openapi.json`
- `GET` and `OPTIONS` on `/swagger`
- all authenticated `/v1.0/*` routes
- `OPTIONS` on every documented non-download route

Intentionally excluded from the generated spec:

- `/download/{slug}`
- `/download/{slug}/sitemap.xml`
- `/download/{slug}/{fileName}`

Those download routes are still live and documented below; they are omitted from Swagger because they are public and dynamic.

## Authentication

All `/v1.0/*` routes require an API key. The server checks headers in this order:

1. `x-api-key`
2. `Authorization: Bearer <key>`

Public routes:

- `/`
- `/openapi.json`
- `/swagger`
- `/download/*`
- `OPTIONS` preflight routes

Example:

```http
x-api-key: mincmsadmin
```

## CORS And Preflight

Every non-download route supports `OPTIONS` and returns `204 No Content` when the origin is allowed.

Default checked-in CORS policy:

```json
{
  "AllowedOrigins": ["*"],
  "AllowedMethods": ["GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE"],
  "AllowedHeaders": ["*"],
  "ExposeHeaders": ["Content-Disposition", "Content-Length", "Content-Type", "ETag"],
  "MaxAgeSeconds": 86400
}
```

Behavior:

- If `Cors` is missing or `null`, MinCMS initializes the object to the permissive defaults above.
- Preflight responses include `Access-Control-Allow-Origin`, `Access-Control-Allow-Methods`, `Access-Control-Allow-Headers`, and `Access-Control-Max-Age`.
- Normal API responses include `Access-Control-Allow-Origin` and `Access-Control-Expose-Headers` when the origin is allowed.
- If a non-wildcard origin list is configured, MinCMS adds `Vary: Origin`.

Typical preflight request:

```http
OPTIONS /v1.0/collections HTTP/1.1
Origin: http://localhost:8300
Access-Control-Request-Method: POST
Access-Control-Request-Headers: x-api-key,content-type
```

Typical preflight response:

```http
HTTP/1.1 204 No Content
Access-Control-Allow-Origin: *
Access-Control-Allow-Methods: GET, HEAD, OPTIONS, POST, PUT, PATCH, DELETE
Access-Control-Allow-Headers: x-api-key,content-type
Access-Control-Max-Age: 86400
Access-Control-Expose-Headers: Content-Disposition, Content-Length, Content-Type, ETag
Allow: GET, HEAD, OPTIONS, POST, PUT, PATCH, DELETE
```

## Error Responses

All JSON errors use this shape:

```json
{
  "error": "AuthenticationFailed",
  "statusCode": 401,
  "message": "Your authentication material was not accepted.",
  "context": null,
  "description": "Authentication required."
}
```

Common error codes:

| Error | HTTP status |
|---|---|
| `AuthenticationFailed` | `401` |
| `BadRequest` | `400` |
| `NotFound` | `404` |
| `Conflict` | `409` |
| `Timeout` | `408` |
| `InternalError` | `500` |

Exception mapping:

| Exception type | Returned error |
|---|---|
| `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`, `FormatException`, `JsonException` | `400 BadRequest` |
| `FileNotFoundException`, `KeyNotFoundException` | `404 NotFound` |
| `InvalidOperationException` | `409 Conflict` |
| `TaskCanceledException`, `OperationCanceledException` | `408 Timeout` |
| any other exception | `500 InternalError` |

## Route Summary

### Documentation And Health

| Method | Path | Auth | Description |
|---|---|---|---|
| `HEAD` | `/` | No | readiness probe |
| `GET` | `/` | No | HTML landing page |
| `OPTIONS` | `/` | No | CORS preflight for root |
| `GET` | `/openapi.json` | No | generated OpenAPI JSON |
| `OPTIONS` | `/openapi.json` | No | CORS preflight for OpenAPI |
| `GET` | `/swagger` | No | Swagger UI |
| `OPTIONS` | `/swagger` | No | CORS preflight for Swagger |

### Collections

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/v1.0/collections` | Yes | list collections |
| `POST` | `/v1.0/collections` | Yes | create collection |
| `OPTIONS` | `/v1.0/collections` | No | CORS preflight |
| `GET` | `/v1.0/collections/{slug}` | Yes | get collection |
| `DELETE` | `/v1.0/collections/{slug}` | Yes | delete collection |
| `OPTIONS` | `/v1.0/collections/{slug}` | No | CORS preflight |

### Files

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/v1.0/collections/{slug}/files` | Yes | list files |
| `POST` | `/v1.0/collections/{slug}/files` | Yes | upload file |
| `DELETE` | `/v1.0/collections/{slug}/files` | Yes | delete multiple files |
| `OPTIONS` | `/v1.0/collections/{slug}/files` | No | CORS preflight |
| `GET` | `/v1.0/collections/{slug}/files/{fileName}` | Yes | get file metadata |
| `DELETE` | `/v1.0/collections/{slug}/files/{fileName}` | Yes | delete one file |
| `OPTIONS` | `/v1.0/collections/{slug}/files/{fileName}` | No | CORS preflight |

### Public Downloads

| Method | Path | Auth | Description |
|---|---|---|---|
| `GET` | `/download/{slug}` | No | HTML directory listing |
| `GET` | `/download/{slug}/sitemap.xml` | No | XML sitemap |
| `GET` | `/download/{slug}/{fileName}` | No | binary file download |

## Endpoint Details

### `HEAD /`

- Auth: none
- Success: `200`
- Body: empty

### `GET /`

- Auth: none
- Success: `200`
- Content type: `text/html`
- Returns: a simple landing page confirming the node is online

### `GET /openapi.json`

- Auth: none
- Success: `200`
- Content type: `application/json`
- Returns: generated OpenAPI document for all non-download routes

### `GET /swagger`

- Auth: none
- Success: `200`
- Content type: `text/html`
- Returns: Swagger UI bound to `/openapi.json`

### `GET /v1.0/collections`

- Auth: required
- Success: `200`
- Response: array of `Collection`

Example:

```json
[
  {
    "Id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "Name": "Product Releases",
    "Slug": "product-releases",
    "CreatedUtc": "2026-05-14T20:00:00Z",
    "IsActive": true
  }
]
```

### `POST /v1.0/collections`

- Auth: required
- Success: `201`
- Request content type: `application/json`
- Request body: `CreateCollectionRequest`
- Response: `Collection`

Example request:

```json
{
  "Name": "Product Releases",
  "Slug": "product-releases"
}
```

### `GET /v1.0/collections/{slug}`

- Auth: required
- Success: `200`
- Response: `Collection`
- `404` if the collection does not exist

### `DELETE /v1.0/collections/{slug}`

- Auth: required
- Success: `204`
- Deletes the collection and all files beneath it

### `GET /v1.0/collections/{slug}/files`

- Auth: required
- Success: `200`
- Response: array of `CollectionFile`

Example:

```json
[
  {
    "Key": "product-releases/installer-v2.0.exe",
    "FileName": "installer-v2.0.exe",
    "Size": 52428800,
    "LastModifiedUtc": "2026-05-14T20:00:00Z",
    "ContentType": "application/octet-stream",
    "ETag": "etag-value"
  }
]
```

### `POST /v1.0/collections/{slug}/files`

- Auth: required
- Success: `201`
- Request content type: `multipart/form-data`
- Response: `CollectionFile`

The upload handler accepts standard browser and `HttpClient` multipart payloads, including filename values carried in `filename` or `filename*`.

Example:

```bash
curl -X POST "http://localhost:8200/v1.0/collections/product-releases/files" \
  -H "x-api-key: mincmsadmin" \
  -F "file=@./installer-v2.0.exe"
```

### `GET /v1.0/collections/{slug}/files/{fileName}`

- Auth: required
- Success: `200`
- Response: `CollectionFile`
- `fileName` must be URL-encoded if it contains reserved characters

### `DELETE /v1.0/collections/{slug}/files`

- Auth: required
- Success: `200`
- Request content type: `application/json`
- Request body: `DeleteFilesRequest`
- Response body: `DeleteFilesResponse`

Example request:

```json
{
  "FileNames": [
    "installer-v1.0.exe",
    "docs/readme.txt"
  ]
}
```

Example:

```bash
curl -X DELETE "http://localhost:8200/v1.0/collections/product-releases/files" \
  -H "x-api-key: mincmsadmin" \
  -H "Content-Type: application/json" \
  -d "{\"FileNames\":[\"installer-v1.0.exe\",\"docs/readme.txt\"]}"
```

Response:

```json
{
  "DeletedCount": 2
}
```

### `DELETE /v1.0/collections/{slug}/files/{fileName}`

- Auth: required
- Success: `204`
- Deletes one file

### `GET /download/{slug}`

- Auth: none
- Success: `200`
- Content type: `text/html`
- Returns: a browsable directory listing for the collection

### `GET /download/{slug}/sitemap.xml`

- Auth: none
- Success: `200`
- Content type: `application/xml`
- Returns: a sitemap listing each downloadable file URL

### `GET /download/{slug}/{fileName}`

- Auth: none
- Success: `200`
- Returns: streamed file content
- Expected response headers:
  - `Content-Type`
  - `Content-Length`
  - `Content-Disposition`

## Data Models

### `Collection`

```json
{
  "Id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "Name": "Product Releases",
  "Slug": "product-releases",
  "CreatedUtc": "2026-05-14T20:00:00Z",
  "IsActive": true
}
```

### `CreateCollectionRequest`

```json
{
  "Name": "Product Releases",
  "Slug": "product-releases"
}
```

### `CollectionFile`

```json
{
  "Key": "product-releases/installer-v2.0.exe",
  "FileName": "installer-v2.0.exe",
  "Size": 52428800,
  "LastModifiedUtc": "2026-05-14T20:00:00Z",
  "ContentType": "application/octet-stream",
  "ETag": "etag-value"
}
```

### `DeleteFilesRequest`

```json
{
  "FileNames": [
    "installer-v1.0.exe",
    "installer-v2.0.exe"
  ]
}
```

### `DeleteFilesResponse`

```json
{
  "DeletedCount": 2
}
```

### `ApiErrorResponse`

```json
{
  "Error": "NotFound",
  "StatusCode": 404,
  "Message": "The requested resource was not found.",
  "Context": null,
  "Description": "Collection with slug 'missing' not found."
}
```

## Postman

The repository includes [MinCMS.postman_collection.json](MinCMS.postman_collection.json) with the default base URL set to `http://localhost:8200`.
