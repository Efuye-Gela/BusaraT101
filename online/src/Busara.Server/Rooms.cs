using System.Globalization;
using Busara.Online;
using Npgsql;

namespace Busara.Server;

public sealed class Rooms(Database database, ServerSettings settings, IGameRandom random)
{
    private static NpgsqlCommand Sql(NpgsqlConnection connection, NpgsqlTransaction? tx, string text,
        params (string Name, object Value)[] values)
    {
        var command = new NpgsqlCommand(text, connection, tx);
        foreach (var value in values) command.Parameters.AddWithValue(value.Name, value.Value);
        return command;
    }

    public static void ValidateCommandId(string? id)
    {
        if (id is null || !Guid.TryParseExact(id, "D", out _))
            throw new ApiException(400, "invalid_command_id", "commandId must be a UUID.");
    }

    private static async Task LockGuest(NpgsqlConnection connection, NpgsqlTransaction tx, Guest guest, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "SELECT id FROM guests WHERE id=@id AND expires_at > clock_timestamp() FOR UPDATE", ("id", guest.Id));
        if (await command.ExecuteScalarAsync(ct) is null)
            throw new ApiException(401, "guest_expired", "This guest session has expired.");
    }

    private static async Task<int> Seat(NpgsqlConnection connection, NpgsqlTransaction? tx, Guid matchId, Guest guest, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "SELECT seat FROM memberships m JOIN guests g ON g.id=m.guest_id " +
            "WHERE m.match_id=@match AND m.guest_id=@guest AND g.expires_at > clock_timestamp()",
            ("match", matchId), ("guest", guest.Id));
        var value = await command.ExecuteScalarAsync(ct);
        if (value is null) throw new ApiException(404, "room_unavailable", "Room unavailable to this guest.");
        return (int)value;
    }

    private static async Task<MatchState> State(NpgsqlConnection connection, NpgsqlTransaction? tx, Guid matchId, bool locked, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "SELECT state::text,version FROM matches WHERE id=@match" + (locked ? " FOR UPDATE" : ""), ("match", matchId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new ApiException(404, "room_unavailable", "Room unavailable.");
        var state = Wire.Decode<MatchState>(reader.GetString(0));
        if (state.schemaVersion != 1 || state.ruleset != "busara-online-mvp-v1" ||
            state.id != matchId.ToString("D") || state.version != reader.GetInt64(1))
            throw new InvalidOperationException("Stored state schema, identity or revision is invalid.");
        DomainRules.ValidateState(state);
        return state;
    }

    private static async Task Event(NpgsqlConnection connection, NpgsqlTransaction tx, MatchState state,
        string kind, string? actionId, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "INSERT INTO match_events(match_id,version,kind,action_id) VALUES(@match,@version,@kind,NULLIF(@action,''))",
            ("match", Guid.Parse(state.id)), ("version", state.version), ("kind", kind), ("action", actionId ?? ""));
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task SaveState(NpgsqlConnection connection, NpgsqlTransaction tx, MatchState state, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "UPDATE matches SET version=@version,state=@state::jsonb WHERE id=@match",
            ("match", Guid.Parse(state.id)), ("version", state.version), ("state", Wire.Encode(state)));
        if (await command.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Match write failed.");
    }

    private static async Task<RoomResult?> GuestReceipt(NpgsqlConnection connection, NpgsqlTransaction tx,
        Guest guest, string commandId, string fingerprint, CancellationToken ct)
    {
        await using var command = Sql(connection, tx,
            "SELECT fingerprint,result::text FROM guest_receipts WHERE guest_id=@guest AND command_id=@command",
            ("guest", guest.Id), ("command", commandId));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        if (!ServerSettings.Equal(fingerprint, reader.GetString(0)))
            throw new ApiException(409, "command_conflict", "This command ID was used for a different request.");
        return Wire.Decode<RoomResult>(reader.GetString(1));
    }

    private static async Task SaveGuestReceipt(NpgsqlConnection connection, NpgsqlTransaction tx,
        Guest guest, string commandId, string fingerprint, string kind, RoomResult result, CancellationToken ct)
    {
        // The invite is derived from the external secret on retries, never stored in plaintext.
        var stored = new RoomResult
        {
            matchId = result.matchId, version = result.version,
            inviteUrl = kind == "create" ? result.inviteUrl[..result.inviteUrl.IndexOf("#", StringComparison.Ordinal)] + "#invite=" : null
        };
        await using var command = Sql(connection, tx,
            "INSERT INTO guest_receipts(guest_id,command_id,fingerprint,kind,result) VALUES(@guest,@command,@fingerprint,@kind,@result::jsonb)",
            ("guest", guest.Id), ("command", commandId), ("fingerprint", fingerprint), ("kind", kind), ("result", Wire.Encode(stored)));
        await command.ExecuteNonQueryAsync(ct);
    }

    private string Invite(Guest guest, string commandId) => settings.Secret("invite-v1", guest.Id.ToString("D") + "\n" + commandId);
    private string InviteUrl(Guest guest, string commandId) =>
        settings.Origin.GetLeftPart(UriPartial.Authority) + "/#invite=" + Invite(guest, commandId);

    public async Task<RoomResult> CreateAsync(Guest guest, CreateRoomRequest request, CancellationToken ct)
    {
        ValidateCommandId(request.CommandId);
        var fingerprint = ServerSettings.Hash("create\n" + Wire.Encode(request));
        await using var connection = await database.Source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await LockGuest(connection, tx, guest, ct);
        var prior = await GuestReceipt(connection, tx, guest, request.CommandId, fingerprint, ct);
        if (prior is not null)
        {
            prior.inviteUrl += Invite(guest, request.CommandId);
            await tx.CommitAsync(ct);
            return prior;
        }
        var matchId = Guid.NewGuid();
        var state = DomainRules.Create(matchId.ToString("D"));
        await using (var command = Sql(connection, tx,
            "INSERT INTO matches(id,version,state) VALUES(@match,@version,@state::jsonb); " +
            "INSERT INTO memberships(match_id,guest_id,seat) VALUES(@match,@guest,0); " +
            "INSERT INTO invitations(token_hash,match_id,expires_at) VALUES(@hash,@match,clock_timestamp()+interval '24 hours')",
            ("match", matchId), ("version", state.version), ("state", Wire.Encode(state)),
            ("guest", guest.Id), ("hash", ServerSettings.Hash(Invite(guest, request.CommandId)))))
            await command.ExecuteNonQueryAsync(ct);
        await Event(connection, tx, state, "RoomCreated", null, ct);
        var result = new RoomResult { matchId = state.id, version = Revision(state), inviteUrl = InviteUrl(guest, request.CommandId) };
        await SaveGuestReceipt(connection, tx, guest, request.CommandId, fingerprint, "create", result, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<RoomResult> JoinAsync(Guest guest, JoinRoomRequest request, CancellationToken ct)
    {
        ValidateCommandId(request.CommandId);
        if (request.InviteToken is null || request.InviteToken.Length != 43)
            throw new ApiException(400, "invite_invalid", "Invitation is invalid or unavailable.");
        var fingerprint = ServerSettings.Hash("join\n" + Wire.Encode(request));
        await using var connection = await database.Source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await LockGuest(connection, tx, guest, ct);
        var prior = await GuestReceipt(connection, tx, guest, request.CommandId, fingerprint, ct);
        if (prior is not null) { await tx.CommitAsync(ct); return prior; }
        var hash = ServerSettings.Hash(request.InviteToken);
        Guid matchId;
        await using (var command = Sql(connection, tx,
            "SELECT match_id FROM invitations WHERE token_hash=@hash", ("hash", hash)))
            matchId = await command.ExecuteScalarAsync(ct) is Guid id ? id
                : throw new ApiException(404, "invite_unavailable", "Invitation is invalid or unavailable.");
        var state = await State(connection, tx, matchId, true, ct);
        await using (var command = Sql(connection, tx,
            "SELECT 1 FROM memberships WHERE match_id=@match AND guest_id=@guest", ("match", matchId), ("guest", guest.Id)))
            if (await command.ExecuteScalarAsync(ct) is not null)
                throw new ApiException(409, "already_joined", "This guest already occupies a seat.");
        await using (var command = Sql(connection, tx,
            "UPDATE invitations SET consumed_by=@guest,consumed_at=clock_timestamp() " +
            "WHERE token_hash=@hash AND consumed_by IS NULL AND expires_at>clock_timestamp()",
            ("guest", guest.Id), ("hash", hash)))
            if (state.phase != "Lobby" || await command.ExecuteNonQueryAsync(ct) != 1)
                throw new ApiException(409, "invite_unavailable", "Invitation is invalid or unavailable.");
        var next = DomainRules.Join(state, 1, "Guest");
        await using (var command = Sql(connection, tx,
            "INSERT INTO memberships(match_id,guest_id,seat) VALUES(@match,@guest,1)", ("match", matchId), ("guest", guest.Id)))
            await command.ExecuteNonQueryAsync(ct);
        await SaveState(connection, tx, next, ct);
        await Event(connection, tx, next, "SeatJoined", null, ct);
        var result = new RoomResult { matchId = next.id, version = Revision(next) };
        await SaveGuestReceipt(connection, tx, guest, request.CommandId, fingerprint, "join", result, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task<ClientView> ViewAsync(Guest guest, Guid matchId, CancellationToken ct)
    {
        await using var connection = await database.Source.OpenConnectionAsync(ct);
        var seat = await Seat(connection, null, matchId, guest, ct);
        return Projection.ForSeat(await State(connection, null, matchId, false, ct), seat);
    }

    public async Task<string> VersionAsync(Guest guest, Guid matchId, CancellationToken ct)
    {
        await using var connection = await database.Source.OpenConnectionAsync(ct);
        await Seat(connection, null, matchId, guest, ct);
        await using var command = Sql(connection, null, "SELECT version FROM matches WHERE id=@match", ("match", matchId));
        return ((long)(await command.ExecuteScalarAsync(ct))!).ToString(CultureInfo.InvariantCulture);
    }

    public async Task<StoredReceipt> ApplyAsync(Guest guest, Guid matchId, OnlineCommand request, CancellationToken ct)
    {
        ValidateCommandId(request.commandId);
        if (request.kind is null || request.kind.Length > 64 || request.name?.Length > 80 ||
            request.decisionId?.Length > 100 || request.expectedVersion?.Length > 20 ||
            request.resourceType is < -1 or > 3 || request.from is < -1 or > 31 || request.to is < -1 or > 31 ||
            request.paymentIds is null || request.paymentIds.Length > 12 ||
            request.paymentIds.Any(id => id is null || id.Length > 100))
            throw new ApiException(400, "invalid_command", "The command payload is invalid.");
        var fingerprint = ServerSettings.Hash(Wire.Encode(request));
        await using var connection = await database.Source.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        var seat = await Seat(connection, tx, matchId, guest, ct);
        var state = await State(connection, tx, matchId, true, ct);
        await Seat(connection, tx, matchId, guest, ct);
        await using (var command = Sql(connection, tx,
            "SELECT guest_id,fingerprint,http_status,receipt::text FROM command_receipts WHERE match_id=@match AND command_id=@command",
            ("match", matchId), ("command", request.commandId)))
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                if (reader.GetGuid(0) != guest.Id || !ServerSettings.Equal(reader.GetString(1), fingerprint))
                    throw new ApiException(409, "command_conflict", "This command ID was used for a different request.");
                var previous = new StoredReceipt(reader.GetInt32(2), Wire.Decode<CommandReceipt>(reader.GetString(3)));
                await reader.DisposeAsync();
                await tx.CommitAsync(ct);
                return previous;
            }
        }
        var receipt = new CommandReceipt
        {
            commandId = request.commandId, matchId = matchId.ToString("D"), version = Revision(state), status = "accepted"
        };
        var httpStatus = 200;
        try
        {
            if (request.expectedVersion != Revision(state))
                throw new RuleException("stale_version", "The room changed. Refresh before choosing another action.");
            var transition = DomainRules.Apply(state, seat, request, random);
            if (transition.state.version != checked(state.version + 1))
                throw new InvalidOperationException("Domain transition did not advance one revision.");
            await SaveState(connection, tx, transition.state, ct);
            await Event(connection, tx, transition.state, transition.eventKind, transition.actionId, ct);
            receipt.version = Revision(transition.state);
        }
        catch (RuleException error)
        {
            httpStatus = 409;
            receipt.status = "rejected";
            receipt.code = error.Code;
            // Never persist arbitrary error details or submitted/private payloads in receipts.
            receipt.message = "Command rejected. Refresh the room and check the available choices.";
        }
        await using (var command = Sql(connection, tx,
            "INSERT INTO command_receipts(match_id,command_id,guest_id,fingerprint,http_status,receipt) " +
            "VALUES(@match,@command,@guest,@fingerprint,@status,@receipt::jsonb)",
            ("match", matchId), ("command", request.commandId), ("guest", guest.Id), ("fingerprint", fingerprint),
            ("status", httpStatus), ("receipt", Wire.Encode(receipt))))
            await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return new StoredReceipt(httpStatus, receipt);
    }

    private static string Revision(MatchState state) => state.version.ToString(CultureInfo.InvariantCulture);
}
