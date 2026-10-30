using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [Test]
    public async Task MissingRegistrationShardFailsWithoutPublicBootstrapOrWrites()
    {
        var missing = new MemoryStore();
        var worker = new MatchService(missing, new FixedRandom(), clock);
        foreach (string operation in new[] { "guest", "register" })
        {
            var reply = await worker.Execute("fresh", operation, "{}", "");
            Assert.That(reply.status, Is.EqualTo(503));
            Assert.That(reply.body, Does.Contain("registration_not_initialized"));
        }
        Assert.That((await worker.Execute("fresh", "bootstrap", "{}", "")).body, Does.Contain("unknown_operation"));
        Assert.That(missing.Ids, Is.Empty);
    }

    [Test]
    public async Task OneActorsBloatedGuestDocumentNeverBlocksAnotherActor()
    {
        var stored = (await store.Read(GuestKey("host")))!;
        var guest = Json.Decode<GuestDocument>(stored.Json);
        guest.creates[new string('x', MatchService.MaximumDocumentBytes)] = new Creation();
        await store.CompareExchange(GuestKey("host"), Json.Encode(guest), stored.WriteLock);
        var hostCreate = await service.Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "");
        Assert.That(hostCreate.status, Is.EqualTo(507), "Host's own oversized ledger blocks only host.");
        var guestCreate = await service.Execute("guest", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "");
        Assert.That(guestCreate.status, Is.EqualTo(200), "A different actor's storage is untouched.");
    }

    [Test]
    public async Task ConcurrentCreatePublishesOneRoomAndNeverReinitializesIt()
    {
        string request = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() });
        store.RaceNextTwoReads(GuestKey("host"));
        var replies = await Task.WhenAll(Worker().Execute("host", "create", request, ""),
            Worker().Execute("host", "create", request, ""));
        Assert.That(replies.All(reply => reply.status == 200), Is.True);
        Assert.That(replies[0].body, Is.EqualTo(replies[1].body));
        Assert.That(store.Conflicts, Is.GreaterThan(0), "Both workers read the same write lock.");
        match = Json.Decode<RoomResult>(replies[0].body).matchId;
        await Apply("host", "configure");
        string advanced = (await store.Read("busara_" + match))!.Json;
        Assert.That((await Worker().Execute("host", "create", request, "")).body, Is.EqualTo(replies[0].body));
        Assert.That((await store.Read("busara_" + match))!.Json, Is.EqualTo(advanced));
        var guest = Json.Decode<GuestDocument>((await store.Read(GuestKey("host")))!.Json);
        foreach (string id in store.Ids.Where(MemoryStore.IsRoom))
        {
            var candidate = Json.Decode<RoomDocument>((await store.Read(id))!.Json);
            if (guest.creates[candidate.creationKey].matchId == candidate.state.id) continue;
            var hidden = await service.Execute("host", "view", "{}", candidate.state.id);
            Assert.That(hidden.status, Is.EqualTo(404), "Orphans cannot be played.");
        }
    }

    [Test]
    public async Task JoinRaceClaimsOnlyOneSeatAndOriginalRetryWorks()
    {
        string original = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite });
        string other = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite });
        var replies = await Task.WhenAll(Worker().Execute("guest", "join", original, ""),
            Worker().Execute("outsider", "join", other, ""));
        Assert.That(replies.Count(reply => reply.status == 200), Is.EqualTo(1));
        int winner = replies[0].status == 200 ? 0 : 1;
        var retry = await Worker().Execute(winner == 0 ? "guest" : "outsider", "join", winner == 0 ? original : other, "");
        Assert.That(retry.body, Is.EqualTo(replies[winner].body));
        Assert.That((await StoredRoom()).events.Count(e => e.kind == "SeatJoined"), Is.EqualTo(1));
    }

    [Test]
    public async Task WrongSeatAndHiddenPayloadsAreNotAuthorizedByInvitePossession()
    {
        Assert.That((await service.Execute("outsider", "view", "{}", match)).status, Is.EqualTo(404));
        await Join();
        await Apply("host", "configure");
        await Apply("guest", "configure");
        var start = await Make("guest", "start");
        Assert.That((await Send("guest", start)).status, Is.EqualTo(409));
        await Apply("host", "start");
        var reply = await service.Execute("host", "view", "{}", match);
        var view = Json.Decode<ClientView>(reply.body);
        Assert.That(view.players[1].kingdom, Is.Null);
        Assert.That(reply.body, Does.Not.Contain("deck").And.Not.Contain("snapshot").And.Not.Contain("members")
            .And.Not.Contain("inviteHash").And.Not.Contain("receipts"));
    }

    [Test]
    public async Task GuestAndInviteExpiryAreFixed()
    {
        string first = (await service.Execute("host", "guest", "{}", "")).body;
        clock.Now = clock.Now.AddDays(1).AddSeconds(1);
        Assert.That((await service.Execute("host", "register", "{}", "")).body, Is.EqualTo(first));
        Assert.That((await service.Execute("guest", "join",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite }), "")).status, Is.EqualTo(409));
        clock.Now = clock.Now.AddDays(30);
        Assert.That((await service.Execute("host", "register", "{}", "")).status, Is.EqualTo(401));
        Assert.That((await service.Execute("host", "view", "{}", match)).status, Is.EqualTo(401));
    }

    [Test]
    public async Task DifferentRoomsDoNotShareCommandsOrState()
    {
        string original = match;
        string before = (await store.Read("busara_" + original))!.Json;
        var second = Json.Decode<RoomResult>((await service.Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "")).body);
        match = second.matchId;
        await Apply("host", "configure");
        Assert.That((await store.Read("busara_" + original))!.Json, Is.EqualTo(before));
    }

    [Test]
    public async Task CapacityRejectsWithoutDiscardingStateReceiptsOrHistory()
    {
        var stored = (await store.Read("busara_" + match))!;
        var room = Json.Decode<RoomDocument>(stored.Json);
        room.events.Add(new HistoryEvent { version = "1", kind = new string('x', MatchService.MaximumDocumentBytes) });
        await store.CompareExchange("busara_" + match, Json.Encode(room), stored.WriteLock);
        string before = (await store.Read("busara_" + match))!.Json;
        var command = await Make("host", "configure");
        command.name = "Ada"; command.ready = true;
        var reply = await Send("host", command);
        Assert.That(reply.status, Is.EqualTo(507));
        Assert.That(reply.body, Does.Contain("storage_capacity_reached"));
        Assert.That((await store.Read("busara_" + match))!.Json, Is.EqualTo(before));
    }

    [Test]
    public async Task ExistingGuestIsNotRenewedByConcurrentRegisterCalls()
    {
        string first = (await service.Execute("host", "guest", "{}", "")).body;
        clock.Now = clock.Now.AddDays(2);
        var replies = await Task.WhenAll(Worker().Execute("host", "register", "{}", ""),
            Worker().Execute("host", "register", "{}", ""));
        Assert.That(replies.Select(reply => reply.body), Is.All.EqualTo(first));
    }

    [Test]
    public async Task ModuleWireEnvelopeKeepsJsonFieldsAndStringRevisions()
    {
        var reply = await service.Execute("host", "view", "{}", match);
        string wire = Newtonsoft.Json.JsonConvert.SerializeObject(new { output = reply });
        using var document = System.Text.Json.JsonDocument.Parse(wire);
        var output = document.RootElement.GetProperty("output");
        Assert.That(output.GetProperty("status").GetInt32(), Is.EqualTo(200));
        var projection = Json.Decode<ClientView>(output.GetProperty("body").GetString()!);
        Assert.That(projection.version, Is.EqualTo("1"));
    }

    [Test]
    public async Task ConcurrentJoinIdCannotClaimDifferentRoomsOrBecomeCreate()
    {
        var second = Json.Decode<RoomResult>((await service.Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "")).body);
        string commandId = Guid.NewGuid().ToString();
        store.RaceNextTwoReads(GuestKey("guest"));
        var replies = await Task.WhenAll(
            Worker().Execute("guest", "join", Json.Encode(new RoomRequest { commandId = commandId, inviteToken = invite }), ""),
            Worker().Execute("guest", "join", Json.Encode(new RoomRequest
            {
                commandId = commandId, inviteToken = second.inviteUrl["#invite=".Length..]
            }), ""));
        Assert.That(replies.Count(reply => reply.status == 200), Is.EqualTo(1));
        Assert.That(replies.Single(reply => reply.status != 200).body, Does.Contain("command_conflict"));
        var other = Json.Decode<RoomDocument>((await store.Read("busara_" + second.matchId))!.Json);
        Assert.That(new[] { (await StoredRoom()).members[1], other.members[1] }.Count(actor => actor == "guest"), Is.EqualTo(1));
        var create = await service.Execute("guest", "create", Json.Encode(new RoomRequest { commandId = commandId }), "");
        Assert.That(create.body, Does.Contain("command_conflict"));
    }
}
