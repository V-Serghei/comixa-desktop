# Comixa Desktop

Comixa Desktop is the desktop companion app for Comixa, an original local-first comic reader. It is not a clone of another reader, service, or platform. The Android app exists separately; this repository focuses on a clean desktop foundation.

## Current Phase

MVP 1 - Local Desktop Reader Core

The first milestone prepares the architecture for a local comic library, supported local reading formats, reading progress, bookmarks, and a desktop reader experience. It also keeps a clean shared data contract for future Android/Desktop sync without implementing network sync.

## Platforms

Comixa Desktop starts Windows-first, while the architecture must stay friendly to Linux and macOS. Shared projects should avoid Windows-only APIs. Future platform-specific code should be isolated behind interfaces.

## Tech Stack

- C# and .NET 8 LTS
- Avalonia UI
- MVVM
- SQLite
- xUnit
- GitHub Actions
- Apache License 2.0

## Planned MVP 1 Features

- Local folder selection and file scanning
- CBZ/ZIP/PDF reading only
- Library screen with covers
- Reader screen with page navigation
- Zoom and fit modes
- Reading progress
- Bookmarks
- SQLite database
- Repository pattern for persistence boundaries

## Intentionally Out Of Scope

- Cloud sources
- SMB or WebDAV
- CBR/RAR
- 7z/CB7
- EPUB
- Nested archives
- Loose image folders
- OCR
- AI translation
- Network sync

## Development Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/Comixa.Desktop/Comixa.Desktop.csproj
dotnet format
```

The repository includes `global.json` to pin development to .NET 8 LTS.

## Repository Structure

```text
src/
  Comixa.Desktop/  Avalonia UI shell and MVVM presentation layer.
  Comixa.Core/     Domain models and shared abstractions.
  Comixa.Data/     SQLite and repository implementations.
  Comixa.Reader/   Comic archive reading and page rendering abstractions.
  Comixa.Sync/     Sync contracts only; no network sync implementation.

tests/
  Comixa.Core.Tests/
  Comixa.Data.Tests/
  Comixa.Reader.Tests/

docs/
  product.md
  architecture.md
  sync-contract-v0.md
  supported-formats.md
  roadmap.md
  git-flow.md
  ai-context.md
```
