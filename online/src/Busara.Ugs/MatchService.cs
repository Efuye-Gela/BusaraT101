using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Busara.Online;

namespace Busara.Ugs;

public sealed class MatchService(IPrivateStore store, IGameRandom random, TimeProvider clock)
{
    public const string DirectoryId = "busara_directory_v1";
    // Leave headroom under Cloud Save's 5 MiB per Custom ID for serialization.
    public const int MaximumDocumentBytes = 2 * 1024 * 1024;
    private const int Attempts = 4;
    private DateTimeOffset Now => clock.GetUtcNow();

    public async Task<Reply> Execute(string? actor, string operation, string payload, string matchId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(actor)) throw new RequestError(401, "authentication_required");
            if (payload is null || payload.Length > 16384) throw new RequestError(400, "invalid_request");
            return operation switch
            {
                "guest" => await Guest(actor, false),
                "register" => await Guest(actor, true),
                "create" => await Create(actor, Json.Decode<RoomRequest>(payload)),
                "join" => await Join(actor, Json.Decode<RoomRequest>(payload)),
                "view" => await View(actor, matchId),
                "command" => await Command(actor, matchId, payload),
                _ => throw new RequestError(400, "unknown_operation")
            };
        }
        catch (RequestError error) { return Reply.Error(error.Status, error.Code); }
        catch (JsonException) { return Reply.Error(400, "invalid_request"); }
    }

    private async Task<(DirectoryDocument Value, string Lock)> Directory()
    {
        var stored = await store.Read(DirectoryId);
        if (stored is null) throw new RequestError(503, "directory_not_initialized");
        var value = DecodeStored<DirectoryDocument>(stored.Json);
        if (value.schemaVersion != 1 || value.guests is null || value.creates is null || value.joins is null)
            throw new InvalidOperationException("Invalid directory schema.");
        return (value, stored.WriteLock);
    }

    private void Authorize(DirectoryDocument directory, string actor)
    {
        if (!directory.guests.TryGetValue(actor, out var expires) || expires <= Now)
            throw new RequestError(401, "guest_expired");
    }

    private static string Encode<T>(T document)
    {
        string json = Json.Encode(document);
        if (Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
            throw new RequestError(507, "storage_capacity_reached");
        return json;
    }

    private static T DecodeStored<T>(string json)
    {
        try { return Json.Decode<T>(json); }
        catch (JsonException) { throw new InvalidOperationException("Stored document schema is invalid."); }
    }

    private async Task<Reply> Guest(string actor, bool register)
    {
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var directory = await Directory();
            if (directory.Value.guests.TryGetValue(actor, out var expires))
            {
                Authorize(directory.Value, actor);
                return GuestReply(actor, expires);
            }
            if (!register) throw new RequestError(401, "guest_unregistered");
            expires = Now.AddDays(30);
            directory.Value.guests.Add(actor, expires);
            if (await store.CompareExchange(DirectoryId, Encode(directory.Value), directory.Lock))
                return GuestReply(actor, expires);
        }
        throw new RequestError(503, "storage_busy");
    }

    private static Reply GuestReply(string actor, DateTimeOffset expires) => Reply.Ok(new GuestView
    {
        guestId = actor, expiresAt = expires.ToString("O"), csrfToken = "ugs-bearer"
    });

    private static void CommandId(string value)
    {
        if (!Guid.TryParseExact(value, "D", out _)) throw new RequestError(400, "invalid_command_id");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string CreationKey(string actor, string command) => Hash(actor + "\n" + command);
    private static string RoomId(string matchId)
    {
        if (!Guid.TryParseExact(matchId, "D", out _)) throw new RequestError(404, "room_unavailable");
        return "busara_" + matchId;
    }

    private async Task<Reply> Create(string actor, RoomRequest request)
    {
        CommandId(request.commandId);
        if (request.inviteToken is not null) throw new RequestError(400, "invalid_request");
        string key = CreationKey(actor, request.commandId);
        Creation? candidate = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var directory = await Directory();
            Authorize(directory.Value, actor);
            if (directory.Value.joins.ContainsKey(key)) throw new RequestError(409, "command_conflict");
            if (directory.Value.creates.TryGetValue(key, out var prior)) return CreationReply(prior);
            RoomDocument? newRoom = null;
            if (candidate is null)
            {
                string id = Guid.NewGuid().ToString("D");
                string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                candidate = new Creation
                {
                    actor = actor, commandId = request.commandId, matchId = id, inviteToken = id + "." + secret
                };
                newRoom = new RoomDocument
                {
                    creationKey = key, members = new[] { actor, "" }, state = DomainRules.Create(id),
                    inviteHash = Hash(candidate.inviteToken), inviteExpiresAt = Now.AddHours(24),
                    events = new List<HistoryEvent> { new() { version = "1", kind = "RoomCreated" } }
                };
            }
            directory.Value.creates.Add(key, candidate);
            string directoryJson = Encode(directory.Value);
            // Publish only after initialization. A losing/crashed creator can leave
            // an inaccessible orphan, never a second published room or a reset state.
            if (newRoom is not null) await store.CreateCandidate(RoomId(candidate.matchId), Encode(newRoom));
            if (await store.CompareExchange(DirectoryId, directoryJson, directory.Lock))
                return CreationReply(candidate);
        }
        throw new RequestError(503, "storage_busy");
    }

    private static Reply CreationReply(Creation creation) => Reply.Ok(new RoomResult
    {
        matchId = creation.matchId, version = "1", inviteUrl = "#invite=" + creation.inviteToken
    });

    private async Task<(RoomDocument Value, string Lock)> Room(string matchId, DirectoryDocument directory)
    {
        var stored = await store.Read(RoomId(matchId));
        if (stored is null) throw new RequestError(404, "room_unavailable");
        var room = DecodeStored<RoomDocument>(stored.Json);
        if (room.schemaVersion != 1 || room.state is null || room.state.id != matchId ||
            room.members is null || room.members.Length != 2 || room.receipts is null || room.events is null ||
            string.IsNullOrEmpty(room.creationKey) || string.IsNullOrEmpty(room.inviteHash))
            throw new InvalidOperationException("Invalid room schema.");
        if (!directory.creates.TryGetValue(room.creationKey, out var published) || published.matchId != matchId)
            throw new RequestError(404, "room_unavailable");
        try { DomainRules.ValidateState(room.state); }
        catch (RuleException) { throw new InvalidOperationException("Stored domain state is invalid."); }
        return (room, stored.WriteLock);
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

    private async Task<Reply> Join(string actor, RoomRequest request)
    {
        CommandId(request.commandId);
        string token = request.inviteToken ?? "";
        if (token.Length != 80 || token[36] != '.') throw new RequestError(404, "invite_unavailable");
        string matchId = token[..36];
        string fingerprint = Hash("join\n" + Json.Encode(request));
        string operationKey = CreationKey(actor, request.commandId);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var directory = await Directory();
            Authorize(directory.Value, actor);
            if (directory.Value.creates.ContainsKey(operationKey) ||
                (directory.Value.joins.TryGetValue(operationKey, out var reserved) && reserved != fingerprint))
                throw new RequestError(409, "command_conflict");
            var room = await Room(matchId, directory.Value);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(room.Value.inviteHash),
                    Encoding.ASCII.GetBytes(Hash(token))))
                throw new RequestError(404, "invite_unavailable");
            var prior = Previous(room.Value, actor, request.commandId, fingerprint);
            if (prior is not null) return prior;
            if (room.Value.members.Contains(actor)) throw new RequestError(409, "already_joined");
            if (room.Value.inviteExpiresAt <= Now || room.Value.members[1] != "" || room.Value.state.phase != "Lobby")
                throw new RequestError(409, "invite_unavailable");
            if (!directory.Value.joins.ContainsKey(operationKey))
            {
                // Reserve request identity before changing the room, so the same
                // guest request cannot concurrently claim two different invitations.
                directory.Value.joins.Add(operationKey, fingerprint);
                if (!await store.CompareExchange(DirectoryId, Encode(directory.Value), directory.Lock)) continue;
            }
            room.Value.state = DomainRules.Join(room.Value.state, 1, "Guest");
            room.Value.members[1] = actor;
            var reply = Reply.Ok(new RoomResult { matchId = matchId, version = Revision(room.Value.state) });
            room.Value.receipts.Add(request.commandId, new Receipt { actor = actor, fingerprint = fingerprint, reply = reply });
            room.Value.events.Add(new HistoryEvent { version = Revision(room.Value.state), kind = "SeatJoined" });
            if (await store.CompareExchange(RoomId(matchId), Encode(room.Value), room.Lock)) return reply;
        }
        throw new RequestError(503, "storage_busy");
    }

    private async Task<Reply> View(string actor, string matchId)
    {
        var directory = await Directory();
        Authorize(directory.Value, actor);
        var room = await Room(matchId, directory.Value);
        return Reply.Ok(Projection.ForSeat(room.Value.state, Seat(room.Value, actor)));
    }

    private async Task<Reply> Command(string actor, string matchId, string payload)
    {
        var request = Json.Decode<OnlineCommand>(payload);
        CommandId(request.commandId);
        if (request.kind is null || request.kind.Length > 64 || request.name?.Length > 80 ||
            request.decisionId?.Length > 100 || request.expectedVersion?.Length > 20 ||
            request.resourceType is < -1 or > 3 || request.from is < -1 or > 31 || request.to is < -1 or > 31 ||
            request.paymentIds is null || request.paymentIds.Length > 12 ||
            request.paymentIds.Any(id => id is null || id.Length > 100))
            throw new RequestError(400, "invalid_command");
        string fingerprint = Hash("command\n" + payload);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var directory = await Directory();
            Authorize(directory.Value, actor);
            var room = await Room(matchId, directory.Value);
            int seat = Seat(room.Value, actor);
            var prior = Previous(room.Value, actor, request.commandId, fingerprint);
            if (prior is not null) return prior;
            var receipt = new CommandReceipt
            {
                commandId = request.commandId, matchId = matchId, version = Revision(room.Value.state), status = "accepted"
            };
            try
            {
                var transition = DomainRules.Apply(room.Value.state, seat, request, random);
                if (transition.state.version != checked(room.Value.state.version + 1))
                    throw new InvalidOperationException("Invalid transition revision.");
                room.Value.state = transition.state;
                receipt.version = Revision(transition.state);
                room.Value.events.Add(new HistoryEvent
                {
                    version = receipt.version, kind = transition.eventKind, actionId = transition.actionId
                });
            }
            catch (RuleException error)
            {
                receipt.status = "rejected";
                receipt.code = error.Code;
                receipt.message = "Command rejected. Refresh and check available choices.";
            }
            var reply = new Reply { status = receipt.status == "accepted" ? 200 : 409, body = Json.Encode(receipt) };
            room.Value.receipts.Add(request.commandId, new Receipt { actor = actor, fingerprint = fingerprint, reply = reply });
            // State, exact receipt, reaction ledger and append-only history share one CAS.
            if (await store.CompareExchange(RoomId(matchId), Encode(room.Value), room.Lock)) return reply;
        }
        throw new RequestError(503, "storage_busy");
    }

    private static string Revision(MatchState state) => state.version.ToString(CultureInfo.InvariantCulture);
}
