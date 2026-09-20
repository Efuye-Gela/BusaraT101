using Busara.Online;

namespace Busara.Ugs;

public sealed partial class MatchService
{
    private async Task<Creation> EnsurePublished(Creation creation)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var stored = await store.Read(RoomId(creation.matchId));
            if (stored is null) throw new InvalidOperationException("Won creation is missing its room document.");
            var room = DecodeStored<RoomDocument>(stored.Json);
            ValidateRoom(room, creation.matchId);
            if (room.creationKey != CreationKey(creation.actor, creation.commandId) ||
                room.members[0] != creation.actor || room.inviteHash != Hash(creation.inviteToken))
                throw new InvalidOperationException("Creation does not match its room.");
            if (room.schemaVersion == 1) await PublishLegacyRoom(room);
            else if (room.published) return creation;
            // Repairs a crash after winning the guest ledger but before publication.
            room.published = true;
            NormalizeReceipts(room);
            if (await store.CompareExchange(RoomId(creation.matchId), Encode(room), stored.WriteLock)) return creation;
        }
        throw new RequestError(503, "storage_busy");
    }

    private static Reply CreationReply(Creation creation) => Reply.Ok(new RoomResult
    {
        matchId = creation.matchId, version = "1", inviteUrl = "#invite=" + creation.inviteToken
    });

    private async Task<(RoomDocument Value, string Lock)> Room(string matchId)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var stored = await store.Read(RoomId(matchId));
            if (stored is null) throw new RequestError(404, "room_unavailable");
            var room = DecodeStored<RoomDocument>(stored.Json);
            ValidateRoom(room, matchId);
            NormalizeReceipts(room);
            if (room.schemaVersion == 1)
            {
                await PublishLegacyRoom(room);
                if (!await store.CompareExchange(RoomId(matchId), Encode(room), stored.WriteLock)) continue;
                // Use the new lock, never the one consumed by migration.
                continue;
            }
            if (!room.published) throw new RequestError(404, "room_unavailable");
            return (room, stored.WriteLock);
        }
        throw new RequestError(503, "storage_busy");
    }

    private static void ValidateRoom(RoomDocument room, string matchId)
    {
        if (room.schemaVersion is not (1 or 2) || room.state is null || room.state.id != matchId ||
            room.members is null || room.members.Length != 2 || room.receipts is null ||
            room.receiptOrder is null || room.events is null ||
            string.IsNullOrEmpty(room.creationKey) || string.IsNullOrEmpty(room.inviteHash))
            throw new InvalidOperationException("Invalid room schema.");
        try { DomainRules.ValidateState(room.state); }
        catch (RuleException) { throw new InvalidOperationException("Stored domain state is invalid."); }
    }

    private static int Seat(RoomDocument room, string actor)
    {
        int seat = Array.IndexOf(room.members, actor);
        if (seat < 0) throw new RequestError(404, "room_unavailable");
        return seat;
    }

    private static Reply? Previous(RoomDocument room, string actor, string commandId, string fingerprint)
    {
        if (!room.receipts.TryGetValue(commandId, out var receipt)) return null;
        if (receipt.actor != actor || receipt.fingerprint != fingerprint)
            throw new RequestError(409, "command_conflict");
        return receipt.reply;
    }

    private static void Retain(RoomDocument room, string commandId, Receipt receipt)
    {
        room.receipts.Add(commandId, receipt);
        room.receiptOrder.Add(commandId);
    }
}
