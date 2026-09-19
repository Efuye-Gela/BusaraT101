using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed class MemoryStore : IPrivateStore
{
    private readonly Dictionary<string, StoredDocument> values = new();
    private readonly object gate = new();
    private long revision;
    private string? raceId;
    private int remainingReaders;
    private TaskCompletionSource? readRace;
    public bool LoseNextRoomAck;
    public int Conflicts;

    public MemoryStore(bool initialized = true)
    {
        if (initialized) values.Add(MatchService.DirectoryId, new StoredDocument(Json.Encode(new DirectoryDocument()), "0"));
    }

    public async Task<StoredDocument?> Read(string id)
    {
        await Task.Yield();
        StoredDocument? value;
        Task? wait = null;
        lock (gate)
        {
            value = values.GetValueOrDefault(id);
            if (raceId == id && readRace is not null)
            {
                wait = readRace.Task;
                if (--remainingReaders == 0)
                {
                    readRace.SetResult();
                    raceId = null;
                    readRace = null;
                }
            }
        }
        if (wait is not null) await wait;
        return value;
    }

    public void RaceNextTwoReads(string id)
    {
        lock (gate)
        {
            raceId = id;
            remainingReaders = 2;
            readRace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public async Task<bool> CompareExchange(string id, string json, string writeLock)
    {
        await Task.Yield();
        lock (gate)
        {
            if (values.GetValueOrDefault(id)?.WriteLock != writeLock) { Conflicts++; return false; }
            values[id] = new StoredDocument(json, (++revision).ToString());
            if (LoseNextRoomAck && id != MatchService.DirectoryId)
            {
                LoseNextRoomAck = false;
                throw new IOException("Simulated lost ACK after commit.");
            }
            return true;
        }
    }

    public async Task CreateCandidate(string id, string json)
    {
        await Task.Yield();
        lock (gate)
        {
            Assert.That(values.ContainsKey(id), Is.False, "Never initialize an existing candidate.");
            values.Add(id, new StoredDocument(json, (++revision).ToString()));
        }
    }

    public string[] Ids { get { lock (gate) return values.Keys.ToArray(); } }
}

public sealed class MatchServiceTests
{
    private sealed class FixedRandom : IGameRandom { public int Next(int maximum) => 0; }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private MemoryStore store = null!;
    private Clock clock = null!;
    private MatchService service = null!;
    private string match = "";
    private string invite = "";
    private MatchService Worker() => new(store, new FixedRandom(), clock);

    [SetUp]
    public async Task Setup()
    {
        store = new MemoryStore();
        clock = new Clock();
        service = Worker();
        foreach (string actor in new[] { "host", "guest", "outsider" })
            Assert.That((await service.Execute(actor, "register", "{}", "")).status, Is.EqualTo(200));
        var room = Json.Decode<RoomResult>((await service.Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "")).body);
        match = room.matchId;
        invite = room.inviteUrl["#invite=".Length..];
    }

    private async Task<RoomDocument> StoredRoom() =>
        Json.Decode<RoomDocument>((await store.Read("busara_" + match))!.Json);

    private async Task<ClientView> View(string actor) =>
        Json.Decode<ClientView>((await service.Execute(actor, "view", "{}", match)).body);

    private async Task Join()
    {
        var reply = await service.Execute("guest", "join",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite }), "");
        Assert.That(reply.status, Is.EqualTo(200), reply.body);
    }

    private async Task<OnlineCommand> Make(string actor, string kind)
    {
        var view = await View(actor);
        return new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString(), expectedVersion = view.version,
            decisionId = view.decision?.id, kind = kind
        };
    }

    private Task<Reply> Send(string actor, OnlineCommand command) =>
        service.Execute(actor, "command", Json.Encode(command), match);

    private async Task Apply(string actor, string kind, int from = -1, int to = -1, int type = -1)
    {
        var command = await Make(actor, kind);
        command.from = from; command.to = to; command.resourceType = type;
        if (kind == "configure") { command.name = actor; command.ready = true; }
        var reply = await Send(actor, command);
        Assert.That(reply.status, Is.EqualTo(200), reply.body);
    }

    [Test]
    public async Task MissingBootstrapNeverInitializesUnlockedDirectory()
    {
        var empty = new MemoryStore(false);
        var reply = await new MatchService(empty, new FixedRandom(), clock).Execute("host", "register", "{}", "");
        Assert.That(reply.status, Is.EqualTo(503));
        Assert.That(reply.body, Does.Contain("directory_not_initialized"));
        Assert.That(empty.Ids, Is.Empty);
    }

    [Test]
    public async Task ConcurrentCreatePublishesOneRoomAndNeverReinitializesIt()
    {
        string request = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() });
        store.RaceNextTwoReads(MatchService.DirectoryId);
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
        var directory = Json.Decode<DirectoryDocument>((await store.Read(MatchService.DirectoryId))!.Json);
        foreach (string id in store.Ids.Where(id => id.StartsWith("busara_") && id != MatchService.DirectoryId))
        {
            var candidate = Json.Decode<RoomDocument>((await store.Read(id))!.Json);
            if (directory.creates[candidate.creationKey].matchId == candidate.state.id) continue;
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
    public async Task DuplicateBeforeStaleAndConflictingReuseNeverDoubleApplies()
    {
        var command = await Make("host", "configure");
        command.name = "Ada"; command.ready = true;
        store.RaceNextTwoReads("busara_" + match);
        var replies = await Task.WhenAll(Worker().Execute("host", "command", Json.Encode(command), match),
            Worker().Execute("host", "command", Json.Encode(command), match));
        Assert.That(replies[0].body, Is.EqualTo(replies[1].body));
        Assert.That(store.Conflicts, Is.GreaterThan(0));
        Assert.That((await StoredRoom()).state.version, Is.EqualTo(2));
        command.name = "Different";
        Assert.That((await Send("host", command)).body, Does.Contain("command_conflict"));
        command.commandId = Guid.NewGuid().ToString();
        Assert.That((await Send("host", command)).body, Does.Contain("stale_version"));
        var state = await StoredRoom();
        Assert.That(state.state.version, Is.EqualTo(2));
        Assert.That(state.events, Has.Count.EqualTo(2));
        Assert.That(state.receipts, Has.Count.EqualTo(2), "Rejected stale commands also have stable receipts.");
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

    [TestCase(true)]
    [TestCase(false)]
    public async Task EarnedOffTurnRetractionSurvivesNewWorkerAndLostAck(bool use)
    {
        await Join();
        await Apply("host", "configure"); await Apply("guest", "configure"); await Apply("host", "start");
        foreach (var p in new[] { (0, 1), (2, 0), (9, 0), (11, 3), (24, 3) })
            await Apply("host", "setupPlace", to: p.Item1, type: p.Item2);
        foreach (var p in new[] { (4, 2), (13, 2), (20, 2), (6, 0), (15, 1) })
            await Apply("guest", "setupPlace", to: p.Item1, type: p.Item2);
        await Apply("host", "move", 24, 25); await Apply("guest", "move", 6, 5);
        await Apply("host", "move", 25, 24); await Apply("guest", "forge", 4, 5);
        await Apply("host", "move", 24, 25); await Apply("guest", "move", 15, 14);
        await Apply("host", "move", 25, 24); await Apply("guest", "forge", 13, 14);
        string beforeBoard = Json.Encode((await StoredRoom()).state.board);
        await Apply("host", "move", 24, 25);
        var before = await StoredRoom();
        string decision = before.state.pending.id;
        string[] payment = before.state.seats[1].virtues.Select(t => t.id).ToArray();
        Assert.That(before.state.seats[1].virtues.Select(t => t.type),
            Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Security }));
        service = Worker();
        Assert.That((await View("guest")).decision.id, Is.EqualTo(decision));
        Assert.That((await View("host")).decision, Is.Null);
        var command = await Make("guest", use ? "use" : "pass");
        command.paymentIds = use ? payment : Array.Empty<string>();
        store.LoseNextRoomAck = true;
        Assert.ThrowsAsync<IOException>(async () => await Send("guest", command));
        service = Worker();
        var reply = await Send("guest", command);
        Assert.That(reply.status, Is.EqualTo(200));
        var after = await StoredRoom();
        Assert.That(after.state.version, Is.EqualTo(before.state.version + 1));
        Assert.That(after.events.Take(before.events.Count).Select(Json.Encode),
            Is.EqualTo(before.events.Select(Json.Encode)));
        Assert.That(after.events, Has.Count.EqualTo(before.events.Count + 1));
        Assert.That(after.state.seats[1].revealed, Is.EqualTo(use));
        if (use)
        {
            Assert.That(after.state.seats[1].virtues, Is.Empty);
            Assert.That(Json.Encode(after.state.board), Is.EqualTo(beforeBoard));
            Assert.That(after.state.reactionPayments.Single().tokens.Select(t => t.id), Is.EquivalentTo(payment));
            await Apply("guest", "ack");
        }
        else Assert.That(after.state.seats[1].virtues.Select(t => t.id), Is.EquivalentTo(payment));
        Assert.That((await StoredRoom()).state.activeSeat, Is.EqualTo(1));
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
        store.RaceNextTwoReads(MatchService.DirectoryId);
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

    [Test]
    public async Task FullDirectoryDoesNotCreateOrphansOnEveryRetry()
    {
        var stored = (await store.Read(MatchService.DirectoryId))!;
        var directory = Json.Decode<DirectoryDocument>(stored.Json);
        directory.guests[new string('x', MatchService.MaximumDocumentBytes)] = clock.Now.AddDays(1);
        await store.CompareExchange(MatchService.DirectoryId, Json.Encode(directory), stored.WriteLock);
        int before = store.Ids.Length;
        var reply = await service.Execute("host", "create",
            Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString() }), "");
        Assert.That(reply.status, Is.EqualTo(507));
        Assert.That(store.Ids, Has.Length.EqualTo(before));
    }
}
