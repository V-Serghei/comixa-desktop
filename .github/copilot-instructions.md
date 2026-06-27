# Comixa Desktop Coding Instructions

Comixa Desktop is an original local-first comic reader companion app. It is not a clone.

- Target .NET 8 and Avalonia UI.
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
