# Architecture

Comixa Desktop uses a small layered architecture built around .NET 8 LTS and Avalonia. The desktop app is Windows-first, while shared code should remain friendly to Linux and macOS.

## Projects

- `Comixa.Desktop`: Avalonia UI app, views, view models, local settings, cover cache, and page preview loading.
- `Comixa.Core`: domain models and repository abstractions.
- `Comixa.Data`: SQLite schema and repository implementations.
- `Comixa.Reader`: local file scanning, title parsing, archive/page abstractions, and reader-oriented logic that has no Avalonia dependency.
- `Comixa.Sync`: shared DTO/contracts for future Android/Desktop sync alignment only.

## Boundaries

- Avalonia UI belongs only in `Comixa.Desktop`.
- Domain models belong in `Comixa.Core`.
- SQLite belongs in `Comixa.Data`.
- Reader/scanning code belongs in `Comixa.Reader`.
- Sync contracts belong in `Comixa.Sync`.
- Shared projects must not depend on Windows-only APIs.
- Filesystem paths are local-only and must use platform-neutral `System.IO` APIs.

## MVP Format Boundary

Official MVP formats are `CBZ`, `ZIP`, and `PDF`. CBR/RAR, 7z/CB7, EPUB, nested archives, and loose image folders are not MVP behavior.

## Persistence

SQLite is the local persistence boundary. Repository interfaces live in `Comixa.Core`; implementations live in `Comixa.Data`.

Sync-ready fields such as `SyncId`, `ContentFingerprint`, `UpdatedAt`, and nullable delete timestamps can exist locally before network sync exists. They must not imply cloud services, fake remote state, or conflict resolution.

## Sync

`Comixa.Sync` is contracts-only. The current sync contract is documented in `docs/sync-contract-v0.md`.

There is no network sync implementation, no cloud service, no background sync job, and no conflict resolution engine in MVP 1.

## UI

Avalonia views bind to view models. View models should coordinate repositories and reader services but should not contain archive parsing implementation details.
