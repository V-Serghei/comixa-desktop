# Sync Contract v0

Comixa has two clients: Android in a separate repository and Desktop in this repository. This document defines the shared local data vocabulary for a future sync design. It is not a network protocol and it does not imply cloud services.

## Scope

- No network sync implementation.
- No cloud services.
- No fake remote state.
- No conflict resolution engine yet.
- No background network jobs.
- Shelves are local-only for now and are not part of v0.

## Rules

- `pageIndex` is always zero-based.
- `filePath` is local-only and must not be used as cross-device identity.
- `syncId` and `contentFingerprint` are separate concepts.
- `syncId` is the stable record identity candidate.
- `contentFingerprint` is the stable content identity candidate.
- Timestamps use UTC milliseconds since Unix epoch.
- Supported v0 comic formats are `CBZ`, `ZIP`, and `PDF`.

## ComicBook

| Field | Sync | Notes |
| --- | --- | --- |
| `localId` | No | Local database id only. Never use for cross-device identity. |
| `syncId` | Yes | Stable UUID/string for the comic record. |
| `contentFingerprint` | Yes | Stable cross-device identity candidate based on content. |
| `title` | Yes | Display title. |
| `seriesName` | Yes | Nullable/optional series grouping label. |
| `issueNumber` | Yes | Nullable/optional parsed issue number. |
| `format` | Yes | One of `CBZ`, `ZIP`, `PDF`. |
| `pageCount` | Yes | Total pages known by the local reader. |
| `addedAtUtcMillis` | Yes | UTC milliseconds. |
| `updatedAtUtcMillis` | Yes | UTC milliseconds. |
| `deletedAtUtcMillis` | Yes | Nullable soft-delete timestamp. |

## ReadingProgress

| Field | Sync | Notes |
| --- | --- | --- |
| `comicSyncId` | Yes | References `ComicBook.syncId`. |
| `pageIndex` | Yes | Zero-based page index. |
| `totalPages` | Yes | Total pages at the time progress was saved. |
| `status` | Yes | One of `UNREAD`, `IN_PROGRESS`, `COMPLETED`. |
| `updatedAtUtcMillis` | Yes | UTC milliseconds. |

## Bookmark

| Field | Sync | Notes |
| --- | --- | --- |
| `syncId` | Yes | Stable UUID/string for the bookmark record. |
| `comicSyncId` | Yes | References `ComicBook.syncId`. |
| `pageIndex` | Yes | Zero-based page index. |
| `label` / `note` | Yes | Optional user text. Desktop currently stores `note`. |
| `createdAtUtcMillis` | Yes | UTC milliseconds. |
| `updatedAtUtcMillis` | Yes | UTC milliseconds. |
| `deletedAtUtcMillis` | Yes | Nullable soft-delete timestamp. |

## LibraryFolder / WatchedFolder

Folder paths are local-only. A future sync implementation may sync a folder record as user intent, but must not assume the same path exists on Android and Desktop.

| Field | Sync | Notes |
| --- | --- | --- |
| `localId` | No | Local database id only. |
| `syncId` | Yes | Stable UUID/string for the folder record. |
| `displayName` | Yes | User-visible folder name. |
| `filePath` | No | Local-only path, never cross-device identity. |
| `addedAtUtcMillis` | Yes | UTC milliseconds. |
| `updatedAtUtcMillis` | Yes | UTC milliseconds. |
| `deletedAtUtcMillis` | Yes | Nullable soft-delete timestamp. |

## Future Work

Real sync can be designed later around explicit product decisions, deterministic conflict rules, privacy, and user trust. This v0 contract only keeps Desktop and Android aligned on shared concepts.
