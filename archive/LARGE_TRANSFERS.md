# Large Transfers In MinCMS / Less3 / S3Server

## Goal

Support drag-and-drop uploads of large files, starting with a 100MB PowerPoint, from the MinCMS dashboard into a collection without depending on a single full-object request succeeding end-to-end.

For browser-facing uploads, the right primitive is S3 multipart upload. HTTP chunked transfer can help on server-to-server hops, but it does not give the browser the retry, resume, and per-part recovery behavior that large uploads need.

## Current End-To-End Path

Today the path is:

1. Browser dashboard posts `multipart/form-data` to MinCMS.
2. MinCMS fully buffers the request body, manually parses the multipart payload, and wraps the file bytes in a `MemoryStream`.
3. MinCMS sends a single `PutObjectAsync` request to Less3.
4. Less3 accepts that request through S3Server's single-object `PUT` path, writes it to a temp file, then writes it again into the final object store.

Large-transfer support is therefore only as strong as the weakest single-request hop.

## Confirmed Failure Points

### Browser / MinCMS Dashboard

- `dashboard/src/utils/api.js` uploads with one `fetch()` call and one `FormData` body. There is no multipart upload session, no per-part retry, and no resume.
- `dashboard/src/components/modals/UploadFileModal.jsx` uploads up to 4 files concurrently. Four large files can multiply pressure on MinCMS memory, Less3 temp storage, and network sockets.
- The modal does not use `AbortController`, so closing the UI does not actually abort in-flight uploads.
- There is no byte-level progress reporting, only file-level pending/uploading/done/failed states.

Implication:
Even a 100MB file depends on one uninterrupted browser-to-MinCMS request. A transient failure restarts the whole upload.

### MinCMS Server

- `src/MinCms.Server/MinCmsApiHost.cs` reads uploads from `req.Http.Request.DataAsBytes` and passes them into `ParseMultipartFormData(...)`.
- `ParseMultipartFormData(...)` in the same file creates a `MemoryStream` over the buffered request body.
- Watson `HttpRequest.DataAsBytes` fully reads and caches the body before route code can process it.
- `src/MinCms.Core/Services/S3Service.cs` uses a single `PutObjectAsync` call for collection file uploads. There is no multipart threshold, no per-part retry, and no upload session state.

Implication:
MinCMS is not streaming uploads through to Less3. The full file is buffered in MinCMS before the AWS SDK hop starts.

### Watson Defaults In Use By MinCMS

MinCMS constructs `new WebserverSettings(host, port, ssl)` and does not expose Watson IO or timeout tuning in `RestSettings`.

Confirmed Watson 7.0.14 defaults:

- `StreamBufferSize = 65536`
- `ReadTimeoutMs = 10000`
- `IdleTimeoutMs = 120000`
- API route timeout disabled by default

Implication:
Large or slow uploads are exposed to dependency-level socket timeouts, and MinCMS currently has no app-level settings surface for them.

### Less3 / S3Server Single `PUT` Path

- `C:\Code\less3\s3server-7.0\src\S3Server\OperationLimitsSettings.cs` defaults `MaxPutObjectSize` to `128MB`.
- `C:\Code\less3\s3server-7.0\src\S3Server\S3Server.cs` rejects `ObjectWrite` requests above that limit with `EntityTooLarge`.
- `C:\Code\less3\less3-2.1\src\Less3\Api\S3\ObjectHandler.cs` writes normal object uploads to a temp file first, then streams that temp file into the bucket storage driver.
- On non-chunked requests, Less3 also reads the request body from `ctx.Request.DataAsBytes`, so the full body may already be buffered before temp-file staging begins.

Implication:

- A single-request upload above 128MB is guaranteed to fail on the Less3 side.
- Even below 128MB, large single `PUT`s still pay for full-body buffering plus temp-file staging.

### Less3 Multipart Exists, But Is Not Ready To Be The Primary Large-Upload Path

Less3 already implements:

- `CreateMultipartUpload`
- `UploadPart`
- `CompleteMultipartUpload`
- `AbortMultipartUpload`
- `ListParts`
- `ListMultipartUploads`

But there are important gaps:

- `UploadPart` appends a new row every time a part is uploaded. Re-uploading part `7` does not replace the prior part `7`.
- The `uploadparts` schemas in Sqlite, MySQL, and PostgreSQL do not enforce uniqueness on `(uploadguid, partnumber)`.
- `CompleteMultipartUpload` ignores the client-supplied part list and ETags. It instead loads all stored parts, sorts them, and requires contiguous part numbers `1..N`.
- `UploadPart` writes the part to disk and then calls `File.ReadAllBytes(partFile)` to compute hashes, which re-reads the entire part into memory.
- `UploadPart.PartLength` is `int`, not `long`, which is below full S3 part-size expectations.
- `CompleteMultipartUpload` concatenates every part into one temp file and then writes that temp file again into final storage, causing full-object extra IO and temp-disk usage during completion.
- Multipart uploads expire after 7 days and cleanup runs hourly, so failed sessions can retain large temp files for a long time.

Implication:
Less3 has multipart APIs, but retrying the same part number, resuming interrupted sessions, and completing very large uploads are not robust enough yet for first-class use.

### Direct Browser-To-Less3 Auth Gap

- `C:\Code\less3\s3server-7.0\src\S3Server\S3Server.cs` supports SigV4 header authentication and recognizes `UNSIGNED-PAYLOAD` and streaming payload markers.
- `C:\Code\less3\s3server-7.0\src\S3Server\S3Request.cs` parses SigV4 from the `Authorization` header.
- I could not find SigV4 query-string presigned URL parsing. Query parsing only surfaces legacy fields like `awsaccesskeyid`, `signature`, and `expires`, and `S3Server.cs` rejects signature v2.

Implication:
Secure browser-direct uploads cannot currently rely on standard SigV4 presigned URLs unless S3Server is extended. Without that, MinCMS would need to sign request headers per upload operation instead.

### Less3 Dashboard Is Not A Drop-In Solution For Large Browser Uploads

- `C:\Code\less3\less3-2.1\dashboard\src\utils\s3Auth.ts` converts `Blob` bodies to `arrayBuffer()` in order to hash them for SigV4.

Implication:
Even Less3's own browser signing helper is currently full-body hashing in JS. It should not be reused unchanged for large uploads.

## What Could Fail Today For A 100MB PowerPoint

A 100MB file is below Less3's 128MB single-`PUT` limit, so it might succeed in favorable conditions. It can still fail because:

- MinCMS buffers the entire upload before forwarding it.
- Multiple concurrent large files magnify memory and temp-file pressure.
- The upload depends on a single browser-to-MinCMS request and a single MinCMS-to-Less3 request both finishing successfully.
- Watson's default 10-second read timeout can hurt slow or stalled connections.
- Less3 still stages the object through temp storage even when the upload does succeed.

So 100MB is not a safe or directly supported capability today. It is only a best-effort outcome on the current single-request pipeline.

## Recommended End State

Use two upload modes:

- Small files: keep the existing MinCMS proxy upload for simplicity.
- Large files: use browser-direct S3 multipart upload to Less3, with MinCMS acting as the control plane and signer.

Why this should be the target:

- The browser gets per-part retry, resume, and progress.
- MinCMS stops being the data-plane bottleneck for large uploads.
- Less3's existing multipart APIs become the main transfer primitive instead of the 128MB-limited single-`PUT` path.

## Improvement Plan

### Phase 1: Make Less3 Multipart Correct And Retry-Safe

This phase is mandatory before MinCMS should depend on Less3 multipart uploads.

1. Make part upload idempotent by `(uploadguid, partnumber)`.
   Delete or replace the existing part record and part file when the same part number is uploaded again.

2. Add uniqueness in every database backend.
   Add a unique constraint or unique index on `(uploadguid, partnumber)`.

3. Honor the client's `CompleteMultipartUpload` payload.
   Validate the supplied part numbers and ETags instead of ignoring them.

4. Stop requiring contiguous `1..N` part numbering.
   Only require the client-supplied completion list to be valid and ordered.

5. Change part sizes from `int` to `long` and database `INT` columns to `BIGINT`.

6. Hash parts while streaming to disk.
   Remove the `File.ReadAllBytes(partFile)` pass.

7. Rework completion to avoid building a full temporary concatenation file.
   Stream parts directly into final object storage or add a storage-driver API that can assemble a final object from part streams.

8. Add multipart-focused tests.
   Required cases:
   - Re-upload same part number replaces old data
   - Complete with explicit ETags
   - Non-contiguous part numbers
   - Multipart object larger than 128MB
   - Abort and cleanup behavior
   - Server restart or resumed multipart session

### Phase 2: Make The MinCMS Proxy Path Stream And Speak Multipart

This phase keeps the current MinCMS API contract usable while removing the worst bottlenecks.

1. Replace `DataAsBytes` multipart parsing in MinCMS with a streaming reader.
   Do not load the full upload into RAM before handing it off.

2. Introduce an upload strategy in MinCMS.
   Example:
   - `< 32MB`: existing single upload path
   - `>= 32MB`: multipart upload to Less3

3. Replace single `PutObjectAsync` for large files with explicit multipart calls.
   Use `CreateMultipartUpload`, `UploadPart`, and `CompleteMultipartUpload` against Less3.

4. If MinCMS must spool, spool to disk rather than RAM.
   Keep memory bounded to a part-sized window.

5. Expose Watson IO tuning in MinCMS settings.
   At minimum:
   - read timeout
   - idle timeout
   - stream buffer size

6. Add upload telemetry.
   Log threshold decisions, part counts, part retries, upload duration, and aborts.

7. Reduce or adapt dashboard concurrency for proxy uploads.
   Four simultaneous large proxy uploads is too aggressive unless the server path becomes fully streaming.

### Phase 3: Add First-Class Browser-Direct Multipart Upload From MinCMS

This is the preferred end state for "directly supported" large uploads.

1. Add MinCMS control-plane endpoints for upload sessions.
   Suggested shape:
   - create upload session
   - sign initiate request
   - sign upload-part request
   - sign complete request
   - abort upload session

2. Upload directly from browser to Less3 using standard S3 multipart APIs.
   Use part sizes like `8MB` to `16MB` and concurrency like `2` to `4`.

3. Use `UNSIGNED-PAYLOAD` for browser-generated signed header requests if MinCMS is the signer.
   That avoids hashing the entire part body in JS before every upload.

4. Do not reuse Less3's current `s3Auth.ts` hashing flow unchanged.
   Large-file paths must not call `Blob.arrayBuffer()` just to build SigV4 headers.

5. Capture part ETags client-side and persist upload state.
   Store enough state to resume an interrupted upload after refresh or transient failure.

6. Keep the current MinCMS upload endpoint as a compatibility path for small files and automation.

### Phase 4: Optionally Add Presigned URL Support To S3Server

This is not strictly required if MinCMS signs request headers per operation, but it is the cleanest browser contract.

1. Add SigV4 query-string auth parsing to S3Server.
2. Validate presigned multipart operations in Less3/S3Server.
3. Let MinCMS return presigned URLs instead of signed headers.

Benefits:

- simpler browser code
- easier use with standard S3 tooling
- less request-specific signing chatter between browser and MinCMS

## Acceptance Criteria

The feature should not be considered complete until all of these work:

1. A 100MB PowerPoint can be uploaded from the MinCMS dashboard reliably on a normal network.
2. A larger file above 128MB succeeds by multipart upload without touching the single-`PUT` limit.
3. Retrying the same part number replaces the prior part cleanly.
4. Closing the upload UI can abort the underlying transfer.
5. Refreshing the browser can resume an in-progress multipart upload.
6. Less3 does not require a full extra temp-file copy of the final assembled object during completion.
7. Server memory stays bounded roughly to a part-sized window rather than whole-object size.
8. Temp-file cleanup is deterministic after abort or failure.

## Practical Recommendation

If only one path is funded, fund this one:

1. Fix Less3 multipart correctness.
2. Add MinCMS browser-direct multipart uploads with MinCMS as signer.
3. Keep the current MinCMS proxy upload only for small files.

That path removes the largest bottlenecks, aligns with S3-native behavior, and gives large-object uploads the retry semantics they actually need.
