using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Busara.Online;

namespace Busara.Ugs;

public sealed partial class MatchService(IPrivateStore store, IGameRandom random, TimeProvider clock)
{
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
                "command" => await Command(actor, matchId, payload, false),
                "commandWithView" => await Command(actor, matchId, payload, true),
                _ => throw new RequestError(400, "unknown_operation")
            };
        }
        catch (RequestError error) { return Reply.Error(error.Status, error.Code); }
        catch (JsonException) { return Reply.Error(400, "invalid_request"); }
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
        // Unity's JsonUtility writes empty strings for unset fields, so empty means absent.
        if (!string.IsNullOrEmpty(request.inviteToken) || !string.IsNullOrEmpty(request.code) ||
            request.seats is < DomainRules.MinimumSeats or > DomainRules.MaximumSeats)
            throw new RequestError(400, "invalid_request");
        string key = CreationKey(actor, request.commandId);
        Creation? candidate = null;
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var guest = await RequireGuest(actor);
            if (guest.Doc.joins.ContainsKey(key)) throw new RequestError(409, "command_conflict");
            if (guest.Doc.creates.TryGetValue(key, out var prior)) return CreationReply(await EnsurePublished(prior));
            RoomDocument? newRoom = null;
            if (candidate is null)
            {
                string id = Guid.NewGuid().ToString("D");
                string secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                string code = NewCode();
                candidate = new Creation
                {
                    actor = actor, commandId = request.commandId, matchId = id, inviteToken = id + "." + secret, code = code
                };
                var members = Enumerable.Repeat("", request.seats).ToArray();
                members[0] = actor;
                newRoom = new RoomDocument
                {
                    creationKey = key, members = members, state = DomainRules.Create(id, request.seats),
                    inviteHash = Hash(candidate.inviteToken), codeHash = Hash(code), code = code,
                    inviteExpiresAt = Now.AddHours(24),
                    events = new List<HistoryEvent> { new() { version = "1", kind = "RoomCreated" } }
                };
            }
            guest.Doc.creates.Add(key, candidate);
            string guestJson = Encode(guest.Doc);
            // Publish only after initialization. A losing/crashed creator can leave
            // an inaccessible orphan, never a second published room or a reset state.
            if (newRoom is not null)
            {
                await store.CreateCandidate(RoomId(candidate.matchId), Encode(newRoom));
                // An orphaned code (a lost creation race) points at an unpublished room, which reads as unavailable.
                await store.CreateCandidate(CodeId(candidate.code), Encode(new CodeDocument { matchId = candidate.matchId }));
            }
            if (await store.CompareExchange(guest.Key, guestJson, guest.Lock))
                return CreationReply(await EnsurePublished(candidate));
        }
        throw new RequestError(503, "storage_busy");
    }

    private async Task<Reply> Join(string actor, RoomRequest request)
    {
        CommandId(request.commandId);
        string token = request.inviteToken ?? "";
        string? code = string.IsNullOrEmpty(request.inviteToken) && !string.IsNullOrEmpty(request.code)
            ? NormalizeCode(request.code) ?? throw new RequestError(404, "invite_unavailable") : null;
        string matchId;
        if (code is not null)
        {
            var index = await store.Read(CodeId(code));
            if (index is null) throw new RequestError(404, "invite_unavailable");
            matchId = DecodeStored<CodeDocument>(index.Json).matchId;
        }
        else
        {
            if (token.Length != 80 || token[36] != '.')
                throw new RequestError(404, "invite_unavailable");
            matchId = token[..36];
        }
        string fingerprint = Hash("join\n" + Json.Encode(request));
        string operationKey = CreationKey(actor, request.commandId);
        for (int attempt = 0; attempt < Attempts; attempt++)
        {
            var guest = await RequireGuest(actor);
            if (guest.Doc.creates.ContainsKey(operationKey) ||
                (guest.Doc.joins.TryGetValue(operationKey, out var reserved) && reserved != fingerprint))
                throw new RequestError(409, "command_conflict");
            var room = await Room(matchId);
            string expected = code is not null ? room.Value.codeHash : room.Value.inviteHash;
            if (expected.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected),
                    Encoding.ASCII.GetBytes(Hash(code ?? token))))
                throw new RequestError(404, "invite_unavailable");
            var prior = Previous(room.Value, actor, request.commandId, fingerprint);
            if (prior is not null) return prior;
            if (room.Value.members.Contains(actor)) throw new RequestError(409, "already_joined");
            int seat = OpenSeat(room.Value);
            if (room.Value.inviteExpiresAt <= Now || seat < 0 || room.Value.state.phase != "Lobby")
                throw new RequestError(409, "invite_unavailable");
            if (!guest.Doc.joins.ContainsKey(operationKey))
            {
                // Reserve request identity before changing the room, so the same
                // guest request cannot concurrently claim two different invitations.
                guest.Doc.joins.Add(operationKey, fingerprint);
                if (!await store.CompareExchange(guest.Key, Encode(guest.Doc), guest.Lock)) continue;
            }
            room.Value.state = DomainRules.Join(room.Value.state, seat, seat == 1 ? "Guest" : "Guest " + (seat + 1));
            room.Value.members[seat] = actor;
            var reply = Reply.Ok(new RoomResult { matchId = matchId, version = Revision(room.Value.state) });
            Retain(room.Value, request.commandId, new Receipt { actor = actor, fingerprint = fingerprint, reply = reply });
            room.Value.events.Add(new HistoryEvent { version = Revision(room.Value.state), kind = "SeatJoined" });
            if (await store.CompareExchange(RoomId(matchId), Encode(room.Value), room.Lock)) return reply;
        }
        throw new RequestError(503, "storage_busy");
    }

    private async Task<Reply> View(string actor, string matchId)
    {
        var guestTask = RequireRegistration(actor);
        var roomTask = Room(matchId);
        await Task.WhenAll(guestTask, roomTask);
        var room = await roomTask;
        return Reply.Ok(MemberView(room.Value, Seat(room.Value, actor)));
    }

    private static string Revision(MatchState state) => state.version.ToString(CultureInfo.InvariantCulture);
}
