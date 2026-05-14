# Editable Text Content Plan

## Document Use

This file is intended to stay in the repo as a living implementation plan.

- Status legend: `[ ]` not started, `[~]` in progress, `[x]` done, `[!]` blocked, `[-]` dropped
- Update `Owner`, `Updated`, and `Notes` columns as work progresses
- Add dated notes to the journal at the end when decisions or blockers appear

| Field | Value |
| --- | --- |
| Owner |  |
| Last Updated | 2026-04-23 |
| Target Release |  |
| Current Recommendation | Implemented V1 uses the existing public download route for text reads and the existing authenticated upload route for create/save, with a simple textarea-based editor modal |

## Goal

Allow a dashboard user to open supported text files from a collection, edit them in a modal, and save them back to S3 as a full object rewrite.

This feature should support common text content types such as:

- `txt`
- `md`
- `js`
- `xml`
- other text-like files detected by MIME type or extension

## Current State

The current implementation already contains most of the storage primitives needed for this feature:

- `dashboard/src/components/CollectionView.jsx`
  - lists files and exposes `download`, `metadata`, and `delete` actions
- `dashboard/src/utils/api.js`
  - supports collection/file list, metadata, upload, and delete calls
- `src/MinCms.Server/MinCmsServer.cs`
  - exposes authenticated file metadata endpoints
  - exposes public download endpoints
  - does not expose an authenticated file-content read/write API for editor usage
- `src/MinCms.Core/Services/S3Service.cs`
  - can download file streams and metadata
  - can overwrite an object with `PutObjectAsync`

Important implementation note:

- the existing upload path already behaves like a full object write to the same S3 key
- because MinCMS is S3-backed, rewriting the entire object for each save is acceptable
- a delete-then-write flow is also acceptable if needed, but direct overwrite is simpler

## Scope

### In Scope

- Edit existing text-like files directly from the dashboard
- Create new text-like files directly from the dashboard
- Read content into a modal
- Save updated content back to the same collection/file path
- Preserve or explicitly set the content type on save
- Support nested file paths such as `docs/readme.md`
- Handle common failure states in the UI

### Out of Scope for V1

- Binary file editing
- Rich text or WYSIWYG editing
- Collaborative editing
- Version history
- Full IDE features

## Recommended Approach

### Editor Recommendation

Start with a plain `textarea`-based modal, not Monaco or CodeMirror.

Why:

- the dashboard currently has no editor dependency
- `dashboard/package.json` is intentionally small
- a plain textarea is enough for `txt`, `md`, `js`, `xml`, `json`, `html`, and similar content
- this keeps bundle size, setup, and styling risk low

### API Recommendation

Add authenticated content endpoints specifically for text editing rather than reusing the public `/download/*` routes.

Why:

- the public download route is anonymous and optimized for download behavior
- editor reads should remain on authenticated admin APIs
- a dedicated API makes room for future ETag conflict handling and cleaner error messages

## Design Options

### Option A: Minimum-Change Path

Add only an authenticated `GET` endpoint for file text content and reuse the existing authenticated upload endpoint for save.

Flow:

1. Dashboard fetches text content from a new authenticated endpoint.
2. Dashboard saves by creating a `File`/`Blob` and posting it to the existing upload route with the same filename.

Pros:

- smallest backend surface area
- save path reuses existing upload behavior
- fast to ship

Cons:

- save intent is hidden behind the upload API
- multipart is awkward for pure text edits
- optimistic concurrency is harder to express cleanly

### Option B: Recommended Path

Add authenticated `GET` and `PUT` content endpoints dedicated to text editing.

Pros:

- clearer contract
- easier to add `ExpectedETag`
- easier to document and reason about
- simpler client code than multipart reuse

Cons:

- slightly more backend work than Option A

## File Type Detection

A file should be considered editable when all of the following are true:

- its MIME type starts with `text/`
- or its extension is in a text allowlist
- and its size is below the editor limit
- and the server can decode it as UTF-8 text

Recommended initial extension allowlist:

- `.txt`
- `.md`
- `.markdown`
- `.js`
- `.mjs`
- `.cjs`
- `.json`
- `.xml`
- `.html`
- `.htm`
- `.css`
- `.svg`
- `.yml`
- `.yaml`
- `.csv`

Recommended initial size limit:

- `1 MiB` for V1

Notes:

- keep the size limit in a shared constant so it can be raised later
- if content type is missing or generic, fall back to extension detection
- if UTF-8 decoding fails, the UI should surface "This file cannot be edited as text"

## Proposed UX

### Entry Point

Add an `Edit` row action in `dashboard/src/components/CollectionView.jsx`.

The action should only render for editable files, not folders.

### Modal Behavior

Create a new modal:

- `dashboard/src/components/modals/EditTextContentModal.jsx`
- `dashboard/src/components/modals/EditTextContentModal.css`

Modal contents:

- file path
- content type
- file size
- last modified timestamp
- optional ETag display for debugging
- large monospace textarea
- dirty state indicator
- loading, saving, and error states

### Save Behavior

On save:

1. Send the updated text to the authenticated content API.
2. Treat save as a full object replacement in S3.
3. Refresh file metadata/list in the collection view.
4. Show success or error feedback.

### Unsaved Changes

Before closing the modal with unsaved changes:

- prompt the user to confirm discard

## Proposed API Shape

### Recommended Read Endpoint

`GET /v1.0/collections/{slug}/files/{fileName}/content`

Auth:

- required

Suggested response:

```json
{
  "FileName": "docs/readme.md",
  "ContentType": "text/markdown",
  "ETag": "\"abc123\"",
  "Size": 128,
  "Encoding": "utf-8",
  "Content": "# Hello"
}
```

### Recommended Write Endpoint

`PUT /v1.0/collections/{slug}/files/{fileName}/content`

Auth:

- required

Suggested request:

```json
{
  "Content": "# Hello",
  "ContentType": "text/markdown",
  "ExpectedETag": "\"abc123\""
}
```

Suggested response:

- updated `CollectionFile` metadata
- or the same content envelope as the read endpoint

### API Notes

- `ExpectedETag` should be optional at first, but strongly recommended
- if the ETag does not match, return `409 Conflict`
- keep the existing upload endpoint intact for file upload workflows
- do not use the public download endpoint for editor reads

## Backend Implementation Notes

### Likely Touchpoints

- `src/MinCms.Server/MinCmsServer.cs`
- `src/MinCms.Core/Services/ICollectionService.cs`
- `src/MinCms.Core/Services/CollectionService.cs`
- `src/MinCms.Core/Services/IS3Service.cs`
- `src/MinCms.Core/Services/S3Service.cs`
- new request/response DTOs under `src/MinCms.Core/`
- `REST_API.md`

### Suggested Service Additions

Add explicit text-content methods instead of overloading metadata methods.

Examples:

- `GetEditableTextFileAsync(...)`
- `UpdateEditableTextFileAsync(...)`

Suggested behavior:

- fetch metadata first
- validate file is text-like and below the configured size threshold
- read the S3 stream as UTF-8
- save by rewriting the entire object with the selected content type
- optionally enforce `ExpectedETag`

### Save Semantics

Recommended V1 behavior:

- save is a full replacement of the object body
- content type is preserved from metadata unless the client explicitly sends a new text content type

Because MinCMS already writes whole objects to S3, this aligns with the current storage model.

## Dashboard Implementation Notes

### Likely Touchpoints

- `dashboard/src/utils/api.js`
- `dashboard/src/components/CollectionView.jsx`
- `dashboard/src/components/modals/Modal.jsx`
- new `EditTextContentModal` component and CSS

### Suggested Client Additions

In `dashboard/src/utils/api.js`:

- `getEditableFileContent(slug, fileName)`
- `updateEditableFileContent(slug, fileName, payload)`

In `dashboard/src/components/CollectionView.jsx`:

- add `Edit` action
- add `canEditFile(file)` helper
- add modal open/close state
- refresh file list after save

### UI Rules

- folders never show `Edit`
- hide `Edit` for files over the size limit
- hide `Edit` for clearly binary content
- if the server rejects the read because the object is not valid text, show a clear error modal/banner

## Risks and Edge Cases

- encoded nested paths such as `docs%2Freadme.md` must continue to route correctly
- old uploads may have missing or incorrect content types
- UTF-8 decoding may fail for some legacy files
- large files can freeze the UI if no size cap exists
- same-file concurrent edits can silently overwrite each other unless ETag checks are used
- line ending normalization should be observed during manual testing

## Validation Plan

The solution file currently contains only `MinCms.Core` and `MinCms.Server`, so manual verification should be treated as required even if automated coverage is added later.

### Manual Test Matrix

| Case | Status | Owner | Notes |
| --- | --- | --- | --- |
| Edit and save `.txt` file | `[ ]` |  |  |
| Edit and save `.md` file | `[ ]` |  |  |
| Edit and save `.js` file | `[ ]` |  |  |
| Edit and save `.xml` file | `[ ]` |  |  |
| Create a brand new text file from the dashboard | `[ ]` |  |  |
| Edit nested path file such as `docs/readme.md` | `[ ]` |  |  |
| Preserve content type after save | `[ ]` |  |  |
| Hide edit action for binary file | `[ ]` |  |  |
| Hide or block edit for oversized text file | `[ ]` |  |  |
| Reject invalid UTF-8 content cleanly | `[ ]` |  |  |
| Conflict path when file changes after open | `[ ]` |  |  |
| Confirm public download reflects saved text | `[ ]` |  |  |

## Implementation Checklist

### Phase 1: Finalize Design

| Task | Status | Owner | Updated | Notes |
| --- | --- | --- | --- | --- |
| Confirm V1 uses plain textarea instead of Monaco/CodeMirror | `[x]` |  | 2026-04-23 | Plain textarea editor modal shipped |
| Confirm editable extension allowlist and size limit | `[x]` |  | 2026-04-23 | Dashboard allowlist added with 1 MiB cap |
| Decide whether `ExpectedETag` is V1 or V1.1 | `[x]` |  | 2026-04-23 | Deferred; not implemented in the reuse-existing-APIs version |
| Decide whether save uses new `PUT` endpoint or existing upload route for the first pass | `[x]` |  | 2026-04-23 | Existing upload route chosen |

### Phase 2: Backend API

| Task | Status | Owner | Updated | Notes |
| --- | --- | --- | --- | --- |
| Add DTOs for editable text content read/write | `[ ]` |  |  |  |
| Add service methods to read text content | `[ ]` |  |  |  |
| Add service methods to update text content | `[ ]` |  |  |  |
| Add authenticated `GET` content route | `[ ]` |  |  |  |
| Add authenticated `PUT` content route if chosen | `[ ]` |  |  |  |
| Add UTF-8 validation and size enforcement | `[ ]` |  |  |  |
| Add `409` path for ETag mismatch if implemented | `[ ]` |  |  |  |
| Update `REST_API.md` | `[ ]` |  |  |  |

### Phase 3: Dashboard UI

| Task | Status | Owner | Updated | Notes |
| --- | --- | --- | --- | --- |
| Add API client methods for text content read/write | `[x]` |  | 2026-04-23 | Implemented on top of existing download/upload routes |
| Add file editability helper in collection view | `[x]` |  | 2026-04-23 | Helper added and reused for row actions |
| Add `Edit` action to file rows | `[x]` |  | 2026-04-23 | Added for editable files only |
| Build `EditTextContentModal.jsx` | `[x]` |  | 2026-04-23 | Supports existing and new text files |
| Add editor modal styling | `[x]` |  | 2026-04-23 | Responsive plain-text editor styling added |
| Implement dirty-state protection on close | `[x]` |  | 2026-04-23 | Discard confirmation added |
| Refresh collection data after successful save | `[x]` |  | 2026-04-23 | Collection view refreshes after save |
| Surface load/save errors clearly | `[x]` |  | 2026-04-23 | Inline error banner added |

### Phase 4: Verification and Release Readiness

| Task | Status | Owner | Updated | Notes |
| --- | --- | --- | --- | --- |
| Run dashboard build | `[x]` |  | 2026-04-23 | `npm.cmd run build` passed |
| Run server build | `[ ]` |  |  |  |
| Execute manual test matrix | `[ ]` |  |  |  |
| Document known limitations | `[ ]` |  |  |  |
| Mark feature ready for merge | `[ ]` |  |  |  |

## Open Questions

| Question | Decision | Owner | Updated | Notes |
| --- | --- | --- | --- | --- |
| Should V1 preserve original content type automatically or let the user edit it? |  |  |  | Recommended: preserve automatically |
| Should V1 support `.html`, `.css`, `.json`, `.yaml`, and `.svg` immediately or only the explicitly requested types? |  |  |  | Recommended: include all text-like formats in allowlist |
| Should save conflict detection be required before merge? |  |  |  | Recommended: yes if effort is low |
| Should the first version allow creating a brand new text file from the dashboard? | Yes |  | 2026-04-23 | Implemented via `New Text File` |

## Recommended Delivery Order

1. Add backend authenticated text-content read path.
2. Add dashboard modal and `Edit` action behind file-type checks.
3. Add authenticated write path and save flow.
4. Add ETag conflict handling if not already included.
5. Run manual validation and update docs.

## Journal

| Date | Author | Note |
| --- | --- | --- |
| 2026-04-23 | Codex | Initial plan created based on current `CollectionView`, `api.js`, `MinCmsServer`, and `S3Service` implementation. |
| 2026-04-23 | Codex | Implemented V1 with no backend changes: existing file text is loaded through `/download/{slug}/{fileName}`, and create/save uses the existing authenticated upload endpoint to write the full object. |
