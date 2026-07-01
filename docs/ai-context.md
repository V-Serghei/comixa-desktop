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

## Current Implementation State

- `Comixa.Desktop` contains the Avalonia shell, MVVM view models, reader UI, library UI, local settings stores, cover cache, and page preview loader.
- `Comixa.Core` contains domain models and repository contracts.
- `Comixa.Data` contains SQLite repository implementations.
- `Comixa.Reader` contains scanning/title parsing and reader abstractions.
- `Comixa.Sync` remains contracts only.

The app currently has local folder scanning, CBZ/ZIP reading, image-folder detection, SQLite persistence, library search/sort, status views, folder views, series grouping/detail, shelves, bookmarks, reading progress, cover thumbnail caching, reader settings, fullscreen mode, hide/show reader preview, zoom/fit modes, page navigation, page cache/prefetch, and next-part prompts for series.

PDF is detected but not a completed reader pipeline. CBR/RAR/7z/EPUB should remain unsupported placeholders until intentionally scoped.

## Current UX/Performance Notes

- Button styling is centralized in `App.axaml` with explicit contrast and hover/pressed states. Avoid black-on-black controls.
- Reader preview visibility and fullscreen are separate behaviors.
- Page and cover loading are Avalonia bitmap concerns and belong in `Comixa.Desktop.Reader`.
- Series detection is heuristic and combines filename parsing with folder context.
- Vertical reader mode still needs virtualization. Large books can remain memory-heavy there.
- Before considering Rust/C++ modules, prioritize profiling, render-size caching, virtualization, and better decode scheduling.
- The user currently prefers to run builds locally; do not run full build/test commands unless asked. Lightweight static checks are acceptable.

## Suggested Next Priorities

1. Have the user build/run and report any compile/runtime failures.
2. Fix reported build/runtime issues before adding more features.
3. Manually QA the main flows: add folder, folder/status/series views, cover cache, open reader, page turn, fullscreen, hide/show reader, settings persistence, next-part prompt.
4. Add a real PDF render pipeline or clearly hide unsupported PDF reading actions.
5. Virtualize the library grid and vertical reader.
6. Add render-size page cache so the reader does not always keep full-resolution pages for screen-sized reading.

## Boundaries

Do not add fake cloud, network sync, OCR, AI translation, SMB/WebDAV, or CBR/RAR support. Do not add feature flags or backwards compatibility shims. Keep the starter code small and production-oriented.
