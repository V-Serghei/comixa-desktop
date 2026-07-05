# Comixa Desktop Coding Instructions

Comixa Desktop is an original local-first comic reader companion app. It is not a clone.

- Target .NET 8 LTS and Avalonia UI.
- Use MVVM in the desktop app.
- Prefer cross-platform .NET APIs.
- Avoid Windows-only APIs unless isolated behind interfaces.
- Keep filesystem paths platform-neutral.
- Use repository pattern boundaries for data access.
- Keep `Comixa.Sync` as DTOs/contracts only until real sync is intentionally designed.
- Do not invent cloud, sync, OCR, AI translation, SMB/WebDAV, CBR/RAR, or network features.
- Do not add feature flags or backwards compatibility shims.
- Keep comments rare; explain only non-obvious WHY.
- Keep code minimal, clean, and production-oriented.
- Do not run full builds/tests unless the user explicitly asks; the user currently builds locally and reports failures.

## Current App Shape

- Desktop app uses Avalonia MVVM, SQLite repositories, local folder scanning, shelves, bookmarks, reading progress, library search/sort/status/folder/series views, cover caching, and a configurable reader.
- Reader work currently supports CBZ/ZIP and PDF through PDFtoImage/PDFium. CBR/RAR, 7z/CB7, EPUB, nested archives, and loose image folders are not MVP behavior unless explicitly scoped later.
- Page loading uses cache/prefetch and cover thumbnails are cached. Preserve this ownership boundary in `Comixa.Desktop.Reader`.
- Series grouping is heuristic and uses parsed issue numbers plus folder context. Keep changes conservative and easy to revise.
- UI button styles live in `App.axaml`. Keep hover/pressed/focus states explicit and high contrast; avoid black controls on black surfaces.
- The reader preview pane can be hidden separately from fullscreen. Do not conflate hide-preview with fullscreen mode.
