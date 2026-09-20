using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
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
    public async Task CommandReplyEmbedsTheActorsOwnUpdatedView()
    {
        var command = await Make("host", "configure");
        command.name = "Ada"; command.ready = true;
        var reply = await service.Execute("host", "commandWithView", Json.Encode(command), match);
        var result = Json.Decode<CommandResult>(reply.body);
        Assert.That(result.receipt.status, Is.EqualTo("accepted"));
        Assert.That(Json.Encode(result.view), Is.EqualTo(Json.Encode(await View("host"))));
    }

    [Test]
    public async Task OtherSeatsCommandsNeverEvictOriginalCommandOrJoinReceipt()
    {
        string join = Json.Encode(new RoomRequest { commandId = Guid.NewGuid().ToString(), inviteToken = invite });
        var joined = await service.Execute("guest", "join", join, "");
        var command = await Make("guest", "configure");
        command.name = "Guest"; command.ready = true;
        var original = await Send("guest", command);
        for (int i = 0; i < 20; i++) await Apply("host", "configure");
        var after = await StoredRoom();
        Assert.That(after.receipts, Has.Count.EqualTo(22));
        Assert.That((await Worker().Execute("guest", "join", join, "")).body, Is.EqualTo(joined.body));
        Assert.That((await Worker().Execute("guest", "command", Json.Encode(command), match)).body, Is.EqualTo(original.body));
        command.name = "Changed";
        Assert.That((await Send("guest", command)).body, Does.Contain("command_conflict"));
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(Json.Encode(after)));
    }
}
