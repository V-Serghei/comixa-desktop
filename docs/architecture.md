# Architecture

Comixa Desktop uses a small layered architecture built around .NET 10 and Avalonia.

## Projects

- `Comixa.Desktop`: Avalonia UI app, views, view models, and desktop composition.
- `Comixa.Core`: domain models and shared repository abstractions.
- `Comixa.Data`: SQLite data access and repository implementations.
- `Comixa.Reader`: comic archive reader abstractions and future page rendering pipeline.
- `Comixa.Sync`: sync DTOs/contracts only; no network sync implementation.

## Rules

- UI code stays in `Comixa.Desktop`.
- Domain types and interfaces stay framework-neutral.
- Data access depends on core abstractions.
- Reader code depends on core models but not the desktop UI.
- Platform-specific APIs must be hidden behind interfaces.
- Filesystem paths must be handled with platform-neutral .NET APIs.

## Persistence

SQLite is the intended local database. Repository interfaces live in `Comixa.Core`; implementations belong in `Comixa.Data`.

## UI

Avalonia views should bind to view models. Keep view models testable and avoid placing persistence or archive parsing directly in views.
