namespace Busara.Ugs;

// This is a read-only migration source, never a destination for new operations.
public sealed class DirectoryDocument
{
    public int schemaVersion = 1;
    public Dictionary<string, DateTimeOffset> guests = new();
    public Dictionary<string, Creation> creates = new();
    public Dictionary<string, string> joins = new();
}

public sealed partial class MatchService
{
    public const string DirectoryId = "busara_directory_v1";

    private async Task<DirectoryDocument?> ReadLegacyDirectory()
    {
        var stored = await store.Read(DirectoryId);
        if (stored is null) return null;
        var document = DecodeStored<DirectoryDocument>(stored.Json);
        if (document.schemaVersion != 1 || document.guests is null ||
            document.creates is null || document.joins is null)
            throw new InvalidOperationException("Invalid legacy directory schema.");
        return document;
    }

    private async Task PublishLegacyRoom(RoomDocument room)
    {
        var directory = await ReadLegacyDirectory();
        if (directory is null || !directory.creates.TryGetValue(room.creationKey, out var creation) ||
            creation.matchId != room.state.id || creation.actor != room.members[0] ||
            CreationKey(creation.actor, creation.commandId) != room.creationKey ||
            Hash(creation.inviteToken) != room.inviteHash)
            throw new RequestError(404, "room_unavailable");
        room.schemaVersion = 2;
        room.published = true;
        room.receiptOrder = room.receipts.Keys.ToList();
    }
}
