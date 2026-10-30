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
        matchId = creation.matchId, version = "1", inviteUrl = "#invite=" + creation.inviteToken,
        code = string.IsNullOrEmpty(creation.code) ? null : creation.code
    });

    // Room codes: 8 characters from an alphabet without look-alikes (31^8, about 8.5e11), so a code cannot
    // practically be guessed within its 24 hours. Joins compare hashes in constant time.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    private static string NewCode()
    {
        var code = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            code[i] = CodeAlphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(code);
    }

    private static string? NormalizeCode(string? code)
    {
        string value = new string((code ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        return value.Length == CodeLength && value.All(c => CodeAlphabet.Contains(c)) ? value : null;
    }

    private static string CodeId(string code) => "busara_code_" + Hash("room-code\n" + code)[..40].ToLowerInvariant();

    // The first empty seat that is neither claimed by a player nor filled by a bot.
    private static int OpenSeat(RoomDocument room) =>
        Enumerable.Range(1, room.members.Length - 1).FirstOrDefault(seat => room.members[seat] == "" &&
            !room.state.seats[seat].joined, -1);

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
            room.members is null || room.members.Length != room.state.seats.Count || room.receipts is null ||
            room.receiptOrder is null || room.events is null ||
            string.IsNullOrEmpty(room.creationKey) || string.IsNullOrEmpty(room.inviteHash))
            throw new InvalidOperationException("Invalid room schema.");
        try { DomainRules.ValidateState(room.state); }
        catch (RuleException) { throw new InvalidOperationException("Stored domain state is invalid."); }
    }

    private static ClientView MemberView(RoomDocument room, int seat)
    {
        ClientView view = Projection.ForSeat(room.state, seat);
        if (room.state.phase == "Lobby" && room.code.Length > 0) view.roomCode = room.code;
        return view;
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
