# Sync Model

Comixa Desktop is local-first. MVP 1 has no network sync implementation.

`Comixa.Sync` exists only to hold future-facing contracts and DTOs so Android and Desktop can share names without pretending sync exists today.

The current shared contract is `docs/sync-contract-v0.md`.

## Current Rules

- No cloud services.
- No network sync.
- No fake remote state.
- No background network jobs.
- No conflict resolution engine yet.

## Future Direction

If sync is added later, it should be designed explicitly around local ownership, deterministic conflict handling, and user trust. Release branches or feature work can add real implementations only after product and architecture decisions are documented.
