# AI Context

Comixa Desktop is an original desktop companion app for Comixa, a local-first comic reader. The Android app exists separately. This repository is not a clone of another reader or service.

## Current Phase

MVP 1 - Local Desktop Reader Core.

## Architectural Preferences

- C# and .NET 10.
- Avalonia UI for cross-platform desktop UI.
- MVVM for presentation logic.
- SQLite for local persistence.
- Repository pattern for data access.
- Platform-neutral filesystem handling.
- Windows-first user experience, but Linux and macOS must remain viable.

Avoid Windows-only APIs in shared code. If platform-specific integration becomes necessary later, hide it behind interfaces and keep implementations isolated.

## Boundaries

Do not add fake cloud, network sync, OCR, AI translation, SMB/WebDAV, or CBR/RAR support. Do not add feature flags or backwards compatibility shims. Keep the starter code small and production-oriented.
