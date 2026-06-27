namespace Comixa.Sync.Contracts;

public interface ISyncContract
{
    SyncAnchor Anchor { get; }

    IReadOnlyList<SyncChange> Changes { get; }
}
