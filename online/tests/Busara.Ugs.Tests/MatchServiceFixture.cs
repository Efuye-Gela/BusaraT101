using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
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
    private string GuestKey(string actor) => store.PublishedGuestKey(actor);

    [SetUp]
    public async Task Setup()
    {
        store = new MemoryStore();
        await store.ProvisionRegistrations();
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
}
