using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Busara.Online;
using Busara.Server;
using Npgsql;
using Xunit;

namespace Busara.Server.Tests;

[Collection("PostgreSQL")]
public sealed class ProtocolTests(PostgresFixture fixture)
{
    private static OnlineCommand Configure(string version, string name = "Player") => new()
    {
        commandId = Guid.NewGuid().ToString("D"), expectedVersion = version, kind = "configure", name = name, ready = true
    };
    private static string Path(RoomResult room) => $"/api/rooms/{room.matchId}";
    private static async Task<(HttpStatusCode Status, CommandReceipt Receipt)> Command(Browser actor, RoomResult room, OnlineCommand command)
    {
        using var response = await actor.SendAsync(HttpMethod.Post, Path(room) + "/commands", command);
        return (response.StatusCode, (await response.Content.ReadFromJsonAsync<CommandReceipt>(Wire.Json))!);
    }

    [Fact]
    public async Task Two_workers_serialize_writes_and_exact_retry_precedes_staleness()
    {
        using var host = await fixture.GuestAsync();
        using var other = await fixture.GuestAsync(fixture.Second);
        var room = await host.CreateAsync();
        await other.JoinAsync(room);
        var view = await host.GetAsync<ClientView>(Path(room));
        var first = Configure(view.version, "Host");
        var second = Configure(view.version, "Guest");
        var raced = await Task.WhenAll(Command(host, room, first), Command(other, room, second));
        Assert.Single(raced, item => item.Status == HttpStatusCode.OK);
        Assert.Single(raced, item => item.Status == HttpStatusCode.Conflict);
        var winner = raced[0].Status == HttpStatusCode.OK ? host : other;
        var winningCommand = raced[0].Status == HttpStatusCode.OK ? first : second;
        var winningReceipt = raced[0].Status == HttpStatusCode.OK ? raced[0].Receipt : raced[1].Receipt;
        winner.Worker = winner.Worker == fixture.First ? fixture.Second : fixture.First;
        var duplicate = await Command(winner, room, winningCommand);
        Assert.Equal(HttpStatusCode.OK, duplicate.Status);
        Assert.Equal(Wire.Encode(winningReceipt), Wire.Encode(duplicate.Receipt));
        using var reordered = await winner.SendAsync(HttpMethod.Post, Path(room) + "/commands", new
        {
            ready = winningCommand.ready, name = winningCommand.name, kind = winningCommand.kind,
            expectedVersion = winningCommand.expectedVersion, commandId = winningCommand.commandId
        });
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        Assert.Equal(Wire.Encode(winningReceipt), Wire.Encode((await reordered.Content.ReadFromJsonAsync<CommandReceipt>(Wire.Json))!));
        var loser = winner == host ? other : host;
        var losingCommand = winner == host ? second : first;
        var rejectedRetry = await Command(loser, room, losingCommand);
        Assert.Equal(HttpStatusCode.Conflict, rejectedRetry.Status);
        Assert.Equal("stale_version", rejectedRetry.Receipt.code);
        Assert.Equal(3L, await fixture.ScalarAsync<long>("SELECT count(*) FROM match_events WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
        Assert.Equal(2L, await fixture.ScalarAsync<long>("SELECT count(*) FROM command_receipts WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
        Assert.Equal(winningReceipt.version, (await host.GetAsync<ClientView>(Path(room))).version);
    }

    [Fact]
    public async Task Receipt_fingerprint_actor_membership_and_separate_matches_are_isolated()
    {
        using var host = await fixture.GuestAsync();
        using var opponent = await fixture.GuestAsync(fixture.Second);
        using var stranger = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        await opponent.JoinAsync(room);
        var separate = await stranger.CreateAsync();
        var current = await host.GetAsync<ClientView>(Path(room));
        var request = Configure(current.version);
        Assert.Equal(HttpStatusCode.OK, (await Command(host, room, request)).Status);
        using var stolen = await stranger.SendAsync(HttpMethod.Post, Path(room) + "/commands", request);
        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        using var adversary = await opponent.SendAsync(HttpMethod.Post, Path(room) + "/commands", request);
        Assert.Equal(HttpStatusCode.Conflict, adversary.StatusCode);
        Assert.Contains("command_conflict", await adversary.Content.ReadAsStringAsync());
        request.name = "Different";
        using var modified = await host.SendAsync(HttpMethod.Post, Path(room) + "/commands", request);
        Assert.Equal(HttpStatusCode.Conflict, modified.StatusCode);
        request.name = "Separate";
        request.expectedVersion = separate.version;
        Assert.Equal(HttpStatusCode.OK, (await Command(stranger, separate, request)).Status);
        Assert.Equal("Player", (await host.GetAsync<ClientView>(Path(room))).players[0].name);
        using var invisible = await stranger.SendAsync(HttpMethod.Get, Path(room));
        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
    }

    [Fact]
    public async Task Create_join_guest_and_receipts_survive_a_new_host_process()
    {
        var initialWorker = await fixture.StartWorkerAsync();
        using var host = await fixture.GuestAsync(initialWorker);
        using var opponent = await fixture.GuestAsync(initialWorker);
        var createId = Guid.NewGuid().ToString("D");
        var joinId = Guid.NewGuid().ToString("D");
        var room = await host.CreateAsync(createId);
        var joined = await opponent.JoinAsync(room, joinId);
        var configured = Configure(joined.version);
        var accepted = await Command(host, room, configured);
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        var before = Wire.Encode(await opponent.GetAsync<ClientView>(Path(room)));
        initialWorker.Dispose();
        host.Worker = opponent.Worker = await fixture.StartWorkerAsync();
        Assert.Equal(Wire.Encode(host.View), Wire.Encode(await host.GetAsync<GuestView>("/api/guest")));
        Assert.Equal(Wire.Encode(room), Wire.Encode(await host.CreateAsync(createId)));
        Assert.Equal(Wire.Encode(joined), Wire.Encode(await opponent.JoinAsync(room, joinId)));
        Assert.Equal(Wire.Encode(accepted.Receipt), Wire.Encode((await Command(host, room, configured)).Receipt));
        Assert.Equal(before, Wire.Encode(await opponent.GetAsync<ClientView>(Path(room))));
        Assert.DoesNotContain(Browser.Token(room),
            await fixture.ScalarAsync<string>("SELECT result::text FROM guest_receipts WHERE guest_id=@guest AND command_id=@id",
                ("guest", Guid.Parse(host.View.guestId)), ("id", createId)));
        var token = host.Cookie!.Split('=')[1];
        Assert.NotEqual(token, await fixture.ScalarAsync<string>("SELECT token_hash FROM guests WHERE id=@id", ("id", Guid.Parse(host.View.guestId))));
    }

    [Fact]
    public async Task Single_use_invite_is_race_safe_and_cannot_reclaim_a_seat()
    {
        using var host = await fixture.GuestAsync();
        using var one = await fixture.GuestAsync();
        using var two = await fixture.GuestAsync(fixture.Second);
        var room = await host.CreateAsync();
        var join = new { commandId = Guid.NewGuid().ToString("D"), inviteToken = Browser.Token(room) };
        var raced = await Task.WhenAll(one.SendAsync(HttpMethod.Post, "/api/rooms/join", join), two.SendAsync(HttpMethod.Post, "/api/rooms/join", join));
        try
        {
            Assert.Single(raced, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(raced, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally { foreach (var response in raced) response.Dispose(); }
        Assert.Equal(2L, await fixture.ScalarAsync<long>("SELECT count(*) FROM memberships WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
        using var reclaim = await host.SendAsync(HttpMethod.Post, "/api/rooms/join", new
        {
            commandId = Guid.NewGuid().ToString("D"), inviteToken = Browser.Token(room)
        });
        Assert.Equal(HttpStatusCode.Conflict, reclaim.StatusCode);
        var expiring = await host.CreateAsync();
        await fixture.ExecuteAsync("UPDATE invitations SET expires_at=clock_timestamp()-interval '1 second' WHERE match_id=@id", ("id", Guid.Parse(expiring.matchId)));
        using var expired = await one.SendAsync(HttpMethod.Post, "/api/rooms/join", new
        {
            commandId = Guid.NewGuid().ToString("D"), inviteToken = Browser.Token(expiring)
        });
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
    }

    [Fact]
    public async Task Auth_expiry_csrf_origin_and_unknown_fields_fail_explicitly()
    {
        using var host = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        using var missingCsrf = await host.SendAsync(HttpMethod.Post, "/api/rooms", new { commandId = Guid.NewGuid().ToString("D") }, csrf: false);
        Assert.Equal(HttpStatusCode.Forbidden, missingCsrf.StatusCode);
        using var crossOrigin = await host.SendAsync(HttpMethod.Post, "/api/rooms", new { commandId = Guid.NewGuid().ToString("D") }, origin: "https://evil.invalid");
        Assert.Equal(HttpStatusCode.Forbidden, crossOrigin.StatusCode);
        using var unknown = await host.SendAsync(HttpMethod.Post, Path(room) + "/commands", new
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = room.version, kind = "configure", seat = 1
        });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        await fixture.ExecuteAsync("UPDATE guests SET expires_at=clock_timestamp()-interval '1 second' WHERE id=@id", ("id", Guid.Parse(host.View.guestId)));
        using var expired = await host.SendAsync(HttpMethod.Get, "/api/guest");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        Assert.Contains("guest_expired", await expired.Content.ReadAsStringAsync());
        using var noRemint = await host.SendAsync(HttpMethod.Post, "/api/guest", new { }, csrf: false);
        Assert.Equal(HttpStatusCode.Unauthorized, noRemint.StatusCode);
        Assert.False(noRemint.Headers.Contains("Set-Cookie"));
        host.Cookie = "__Host-busara=" + ServerSettings.NewToken();
        using var invalid = await host.SendAsync(HttpMethod.Post, "/api/guest", new { }, csrf: false);
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.False(invalid.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Guest_cookie_is_secure_fixed_lifetime_and_only_explicit_bootstrap_sets_it()
    {
        using var browser = new Browser(fixture.First, PostgresFixture.NewHttpClient());
        using var absent = await browser.SendAsync(HttpMethod.Get, "/api/guest", csrf: false);
        Assert.Equal(HttpStatusCode.Unauthorized, absent.StatusCode);
        Assert.False(absent.Headers.Contains("Set-Cookie"));
        using var created = await browser.SendAsync(HttpMethod.Post, "/api/guest", new { }, csrf: false);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var cookie = created.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("__Host-busara=", cookie);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        browser.Cookie = cookie.Split(';')[0];
        browser.View = (await created.Content.ReadFromJsonAsync<GuestView>(Wire.Json))!;
        var expiry = DateTimeOffset.Parse(browser.View.expiresAt);
        Assert.InRange(expiry - DateTimeOffset.UtcNow, TimeSpan.FromDays(29.99), TimeSpan.FromDays(30));
        using var restored = await browser.SendAsync(HttpMethod.Get, "/api/guest");
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False(restored.Headers.Contains("Set-Cookie"));
        Assert.Equal(Wire.Encode(browser.View), Wire.Encode((await restored.Content.ReadFromJsonAsync<GuestView>(Wire.Json))!));
        using var repeated = await browser.SendAsync(HttpMethod.Post, "/api/guest", new { }, csrf: false);
        Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        Assert.False(repeated.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Wrong_seat_start_and_failed_transition_leave_state_events_unchanged()
    {
        using var host = await fixture.GuestAsync();
        using var opponent = await fixture.GuestAsync(fixture.Second);
        var room = await host.CreateAsync();
        await opponent.JoinAsync(room);
        var view = await host.GetAsync<ClientView>(Path(room));
        var hostReady = await Command(host, room, Configure(view.version, "Host"));
        var otherReady = await Command(opponent, room, Configure(hostReady.Receipt.version, "Other"));
        Assert.Equal(HttpStatusCode.OK, otherReady.Status);
        var start = new OnlineCommand { commandId = Guid.NewGuid().ToString("D"), expectedVersion = otherReady.Receipt.version, kind = "start" };
        var before = await fixture.ScalarAsync<string>("SELECT state::text FROM matches WHERE id=@id", ("id", Guid.Parse(room.matchId)));
        var events = await fixture.ScalarAsync<long>("SELECT count(*) FROM match_events WHERE match_id=@id", ("id", Guid.Parse(room.matchId)));
        var wrongSeat = await Command(opponent, room, start);
        Assert.Equal(HttpStatusCode.Conflict, wrongSeat.Status);
        Assert.Equal(before, await fixture.ScalarAsync<string>("SELECT state::text FROM matches WHERE id=@id", ("id", Guid.Parse(room.matchId))));
        Assert.Equal(events, await fixture.ScalarAsync<long>("SELECT count(*) FROM match_events WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
        start.commandId = Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.OK, (await Command(host, room, start)).Status);
        var own = await host.GetAsync<ClientView>(Path(room));
        var other = await opponent.GetAsync<ClientView>(Path(room));
        Assert.False(string.IsNullOrEmpty(own.players[0].kingdom));
        Assert.True(string.IsNullOrEmpty(own.players[1].kingdom));
        Assert.True(string.IsNullOrEmpty(other.players[0].kingdom));
        Assert.False(string.IsNullOrEmpty(other.players[1].kingdom));
        var raw = Wire.Encode(own);
        Assert.DoesNotContain("\"deck\"", raw);
        Assert.DoesNotContain("\"snapshot\"", raw);
    }

    [Fact]
    public async Task Websocket_invalidations_cross_workers_contain_only_ids_and_version_and_enforce_csrf()
    {
        using var host = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        using var websocketHttp = new HttpMessageInvoker(PostgresFixture.NewHttpHandler());
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Cookie", host.Cookie);
        socket.Options.SetRequestHeader("Origin", fixture.Second.Origin.GetLeftPart(UriPartial.Authority));
        socket.Options.AddSubProtocol("busara.v1");
        socket.Options.AddSubProtocol("csrf." + host.View.csrfToken);
        var url = new UriBuilder(fixture.Second.Origin) { Scheme = "wss", Path = Path(room) + "/events" }.Uri;
        Assert.True(PostgresFixture.IsLocalTlsTarget(url));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await socket.ConnectAsync(url, websocketHttp, timeout.Token);
        var buffer = new byte[4096];
        var initial = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.Equal(WebSocketMessageType.Text, initial.MessageType);
        using (var json = JsonDocument.Parse(buffer.AsMemory(0, initial.Count)))
            Assert.Equal(room.version, json.RootElement.GetProperty("version").GetString());
        var command = await Command(host, room, Configure(room.version));
        var update = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        using (var json = JsonDocument.Parse(buffer.AsMemory(0, update.Count)))
        {
            Assert.Equal(command.Receipt.version, json.RootElement.GetProperty("version").GetString());
            Assert.Equal(new[] { "matchId", "version" }, json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
        }
        using var badSocket = new ClientWebSocket();
        badSocket.Options.SetRequestHeader("Cookie", host.Cookie);
        badSocket.Options.SetRequestHeader("Origin", fixture.Second.Origin.GetLeftPart(UriPartial.Authority));
        badSocket.Options.AddSubProtocol("busara.v1");
        await Assert.ThrowsAsync<WebSocketException>(() => badSocket.ConnectAsync(url, websocketHttp, timeout.Token));
        await fixture.ExecuteAsync("UPDATE guests SET expires_at=clock_timestamp()-interval '1 second' WHERE id=@id", ("id", Guid.Parse(host.View.guestId)));
        var closed = await socket.ReceiveAsync(buffer.AsMemory(), timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, closed.MessageType);
    }

    [Fact]
    public async Task Independent_matches_progress_while_another_match_is_locked()
    {
        using var blocked = await fixture.GuestAsync();
        using var independent = await fixture.GuestAsync(fixture.Second);
        var lockedRoom = await blocked.CreateAsync();
        var freeRoom = await independent.CreateAsync();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT id FROM matches WHERE id=@id FOR UPDATE", connection, transaction))
        {
            command.Parameters.AddWithValue("id", Guid.Parse(lockedRoom.matchId));
            await command.ExecuteScalarAsync();
        }
        var waiting = Command(blocked, lockedRoom, Configure(lockedRoom.version));
        try
        {
            var result = await Command(independent, freeRoom, Configure(freeRoom.version)).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(HttpStatusCode.OK, result.Status);
            Assert.False(waiting.IsCompleted);
        }
        finally { await transaction.RollbackAsync(); }
        Assert.Equal(HttpStatusCode.OK, (await waiting).Status);
    }

    [Fact]
    public async Task Database_failure_after_state_write_rolls_back_state_event_and_receipt_before_retry()
    {
        using var host = await fixture.GuestAsync();
        var room = await host.CreateAsync();
        var command = Configure(room.version);
        var before = await fixture.ScalarAsync<string>("SELECT state::text FROM matches WHERE id=@id", ("id", Guid.Parse(room.matchId)));
        // Fault injection is confined to the disposable test schema, not exposed through any server route.
        await fixture.ExecuteAsync($"ALTER TABLE match_events ADD CONSTRAINT integration_reject_event CHECK (match_id <> '{Guid.Parse(room.matchId):D}'::uuid OR kind='RoomCreated')");
        try
        {
            using var failure = await host.SendAsync(HttpMethod.Post, Path(room) + "/commands", command);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);
            Assert.DoesNotContain("integration_reject_event", await failure.Content.ReadAsStringAsync());
            Assert.Equal(before, await fixture.ScalarAsync<string>("SELECT state::text FROM matches WHERE id=@id", ("id", Guid.Parse(room.matchId))));
            Assert.Equal(0L, await fixture.ScalarAsync<long>("SELECT count(*) FROM command_receipts WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
            Assert.Equal(1L, await fixture.ScalarAsync<long>("SELECT count(*) FROM match_events WHERE match_id=@id", ("id", Guid.Parse(room.matchId))));
        }
        finally { await fixture.ExecuteAsync("ALTER TABLE match_events DROP CONSTRAINT integration_reject_event"); }
        Assert.Equal(HttpStatusCode.OK, (await Command(host, room, command)).Status);
    }

    [Fact]
    public async Task Pending_draw_decision_survives_process_restart_with_same_owner_and_legal_choices()
    {
        var worker = await fixture.StartWorkerAsync();
        using var host = await fixture.GuestAsync(worker);
        using var opponent = await fixture.GuestAsync(worker);
        var room = await host.CreateAsync();
        var joined = await opponent.JoinAsync(room);
        var readyHost = await Command(host, room, Configure(joined.version, "Host"));
        var readyOther = await Command(opponent, room, Configure(readyHost.Receipt.version, "Other"));
        Assert.Equal(HttpStatusCode.OK, (await Command(host, room, new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = readyOther.Receipt.version, kind = "start"
        })).Status);
        for (var step = 0; step < 10; step++)
        {
            var hostView = await host.GetAsync<ClientView>(Path(room));
            var actor = hostView.activeSeat == 0 ? host : opponent;
            var actorView = await actor.GetAsync<ClientView>(Path(room));
            var choice = actorView.choices.First(item => item.kind == "setupPlace");
            var setup = new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString("D"), expectedVersion = actorView.version, kind = choice.kind,
                from = choice.from, to = choice.to, resourceType = choice.resourceType
            };
            Assert.Equal(HttpStatusCode.OK, (await Command(actor, room, setup)).Status);
        }
        var active = await host.GetAsync<ClientView>(Path(room));
        var owner = active.activeSeat == 0 ? host : opponent;
        var nonOwner = owner == host ? opponent : host;
        Assert.Equal(HttpStatusCode.OK, (await Command(owner, room, new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = active.version, kind = "draw"
        })).Status);
        var pending = await owner.GetAsync<ClientView>(Path(room));
        Assert.Equal("PlaceDraw", pending.decision.kind);
        var waiting = await nonOwner.GetAsync<ClientView>(Path(room));
        Assert.Null(waiting.decision);
        Assert.Empty(waiting.choices);
        worker.Dispose();
        host.Worker = opponent.Worker = await fixture.StartWorkerAsync();
        Assert.Equal(Wire.Encode(pending), Wire.Encode(await owner.GetAsync<ClientView>(Path(room))));
        Assert.Equal(Wire.Encode(waiting), Wire.Encode(await nonOwner.GetAsync<ClientView>(Path(room))));
        var placement = new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = pending.version, kind = "place",
            decisionId = pending.decision.id, to = pending.choices.First(item => item.kind == "place").to
        };
        Assert.Equal(HttpStatusCode.Conflict, (await Command(nonOwner, room, placement)).Status);
        placement.commandId = Guid.NewGuid().ToString("D");
        Assert.Equal(HttpStatusCode.OK, (await Command(owner, room, placement)).Status);
    }

    [Fact]
    public async Task Normally_earned_Retraction_payment_and_undo_survive_a_real_process_restart()
    {
        var worker = await fixture.StartWorkerAsync();
        using var host = await fixture.GuestAsync(worker);
        using var mask = await fixture.GuestAsync(worker);
        // The Testing-only RNG leaves the authored deal order intact; all progression still uses authenticated commands.
        var room = await host.CreateAsync();
        var joined = await mask.JoinAsync(room);
        var readyHost = await Command(host, room, Configure(joined.version, "Host"));
        var readyMask = await Command(mask, room, Configure(readyHost.Receipt.version, "Mask player"));
        Assert.Equal(HttpStatusCode.OK, (await Command(host, room, new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = readyMask.Receipt.version, kind = "start"
        })).Status);
        var deal = await mask.GetAsync<ClientView>(Path(room));
        Assert.Equal(Definitions.KingdomName(Definitions.Mask), deal.players[1].kingdom);
        async Task<CommandReceipt> Send(Browser actor, string kind, int from = -1, int to = -1, int resourceType = -1)
        {
            var view = await actor.GetAsync<ClientView>(Path(room));
            var response = await Command(actor, room, new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString("D"), expectedVersion = view.version, kind = kind,
                from = from, to = to, resourceType = resourceType, decisionId = view.decision?.id
            });
            Assert.Equal(HttpStatusCode.OK, response.Status);
            return response.Receipt;
        }
        for (var count = 0; count < 5; count++)
        {
            var choice = (await host.GetAsync<ClientView>(Path(room))).choices.First(item => item.kind == "setupPlace");
            await Send(host, choice.kind, to: choice.to, resourceType: choice.resourceType);
        }
        foreach (var placement in new[]
        {
            (Slot: 4, Type: ResourceType.Fire), (Slot: 13, Type: ResourceType.Fire),
            (Slot: 20, Type: ResourceType.Fire), (Slot: 6, Type: ResourceType.Water), (Slot: 15, Type: ResourceType.Earth)
        })
            await Send(mask, "setupPlace", to: placement.Slot, resourceType: (int)placement.Type);
        var hostSource = 0;
        async Task HostMove()
        {
            await Send(host, "move", hostSource, 1 - hostSource);
            hostSource = 1 - hostSource;
        }
        await HostMove();
        await Send(mask, "move", 6, 5);
        await HostMove();
        await Send(mask, "forge", 4, 5);
        await HostMove();
        await Send(mask, "move", 15, 14);
        await HostMove();
        await Send(mask, "forge", 13, 14);
        var paidBefore = (await mask.GetAsync<ClientView>(Path(room))).players[1].virtues;
        Assert.Equal(new[] { VirtueType.Art, VirtueType.Security }, paidBefore.Select(token => token.type).Order().ToArray());
        var beforeAction = await host.GetAsync<ClientView>(Path(room));
        await HostMove();
        var pending = await mask.GetAsync<ClientView>(Path(room));
        Assert.Equal("Retraction", pending.decision.kind);
        Assert.Equal(1, pending.decision.owner);
        Assert.Equal(paidBefore.Select(token => token.id).Order(), pending.decision.paymentOptions.Select(token => token.id).Order());
        var waiting = await host.GetAsync<ClientView>(Path(room));
        Assert.Null(waiting.decision);
        Assert.Empty(waiting.choices);
        worker.Dispose();
        host.Worker = mask.Worker = await fixture.StartWorkerAsync();
        Assert.Equal(Wire.Encode(pending), Wire.Encode(await mask.GetAsync<ClientView>(Path(room))));
        Assert.Equal(Wire.Encode(waiting), Wire.Encode(await host.GetAsync<ClientView>(Path(room))));
        var retract = new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString("D"), expectedVersion = pending.version, kind = "use",
            decisionId = pending.decision.id, paymentIds = pending.decision.paymentOptions.Select(token => token.id).ToArray()
        };
        Assert.Equal(HttpStatusCode.Conflict, (await Command(host, room, retract)).Status);
        retract.commandId = Guid.NewGuid().ToString("D");
        var committed = await Command(mask, room, retract);
        Assert.Equal(HttpStatusCode.OK, committed.Status);
        mask.Worker = fixture.Second;
        Assert.Equal(Wire.Encode(committed.Receipt), Wire.Encode((await Command(mask, room, retract)).Receipt));
        var restored = await mask.GetAsync<ClientView>(Path(room));
        Assert.Equal("RetractionNotice", restored.decision.kind);
        Assert.Empty(restored.players[1].virtues);
        Assert.True(restored.players[1].revealed);
        Assert.Equal(Wire.Encode(beforeAction.board), Wire.Encode(restored.board));
        Assert.Equal(1L, await fixture.ScalarAsync<long>(
            "SELECT count(*) FROM match_events WHERE match_id=@id AND kind='ActionRetracted' AND action_id IS NOT NULL",
            ("id", Guid.Parse(room.matchId))));
        await Send(mask, "ack");
        var continued = await mask.GetAsync<ClientView>(Path(room));
        Assert.Equal(1, continued.activeSeat);
        Assert.Contains(continued.choices, choice => choice.kind == "move");
    }
}
