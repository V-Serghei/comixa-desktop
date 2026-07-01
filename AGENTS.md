# Agent Instructions

Comixa Desktop is an original desktop companion app for Comixa, a local-first comic reader. It is not a clone.

## Engineering Direction

- Target C#/.NET 10 and Avalonia UI.
- Prefer cross-platform .NET and Avalonia patterns.
- Use MVVM for UI code.
- Keep filesystem paths platform-neutral with `System.IO` APIs such as `Path.Combine` or `Path.Join`.
- Avoid Windows-only APIs unless they are isolated behind interfaces in platform-specific code.
- Keep shared projects free of UI framework dependencies unless the dependency belongs there.
- Use repository interfaces for persistence boundaries.
- Keep code minimal, clean, and production-oriented.
- Do not run full builds/tests unless the user asks; the current workflow is user-builds-locally. Lightweight checks such as `git diff --check` are okay.

## Current Scope

MVP 1 is the Local Desktop Reader Core. Current work includes local folder scanning, CBZ/ZIP reading, image-folder detection, SQLite persistence, shelves, bookmarks, reading progress, library/search/sort/status/folder/series views, cover caching, reader navigation, zoom/fit modes, fullscreen reader, reader settings, page cache/prefetch, and series next-part prompts.

PDF is detected but not a finished reader pipeline yet. CBR/RAR/7z/EPUB can be detected as unsupported formats, but must not be implemented unless deliberately scoped later.

Do not add fake cloud, sync, OCR, AI translation, SMB/WebDAV, CBR/RAR, or network features. `Comixa.Sync` is contracts only for now.

## Current Design Notes

- Keep reader performance work focused on cache/prefetch strategy, render-size caching, and UI virtualization before considering native modules.
- Page and cover loading live in `Comixa.Desktop.Reader`; do not leak Avalonia bitmap concerns into core/domain projects.
- Series grouping is heuristic: prefer parsed issue numbers and folder context, and keep it easy to revise.
- Reader and chrome button styles are centralized in `App.axaml`. Do not reintroduce black-on-black hover states; use explicit contrast, hover, pressed, and focus states.
- The right reader preview can be hidden; fullscreen is a separate action.

## Style

- No feature flags or backwards compatibility shims in this fresh repository.
- Do not add comments unless the WHY is non-obvious.
- Prefer boring, stable architecture over clever abstractions.
- Keep tests focused on real behavior.
