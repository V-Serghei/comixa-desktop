# Agent Instructions

Comixa Desktop is an original desktop companion app for Comixa, a local-first comic reader. It is not a clone.

## Engineering Direction

- Prefer cross-platform .NET and Avalonia patterns.
- Use MVVM for UI code.
- Keep filesystem paths platform-neutral with `System.IO` APIs such as `Path.Combine` or `Path.Join`.
- Avoid Windows-only APIs unless they are isolated behind interfaces in platform-specific code.
- Keep shared projects free of UI framework dependencies unless the dependency belongs there.
- Use repository interfaces for persistence boundaries.
- Keep code minimal, clean, and production-oriented.

## Current Scope

MVP 1 is the Local Desktop Reader Core. Prepare for local folder scanning, CBZ/ZIP reading, library covers, reader navigation, zoom and fit modes, reading progress, bookmarks, SQLite, and repositories.

Do not add fake cloud, sync, OCR, AI translation, SMB/WebDAV, CBR/RAR, or network features. `Comixa.Sync` is contracts only for now.

## Style

- No feature flags or backwards compatibility shims in this fresh repository.
- Do not add comments unless the WHY is non-obvious.
- Prefer boring, stable architecture over clever abstractions.
- Keep tests focused on real behavior.
