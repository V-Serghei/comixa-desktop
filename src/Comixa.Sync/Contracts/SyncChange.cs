namespace Comixa.Sync.Contracts;

public sealed record SyncChange(
    Guid EntityId,
    string EntityType,
    string ChangeType,
    DateTimeOffset ChangedAt);
