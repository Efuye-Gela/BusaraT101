using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    private async Task EarnOrdinaryPayment()
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
        Assert.That((await StoredRoom()).state.ruleset, Is.EqualTo(Definitions.CurrentRuleset));
        Assert.That((await StoredRoom()).state.seats[1].virtues, Has.Count.EqualTo(2));
    }

    private async Task<Reply> OrdinaryLostAck(string actor, OnlineCommand command)
    {
        var before = await StoredRoom();
        store.LoseNextRoomAck = true;
        Assert.ThrowsAsync<IOException>(async () => await Send(actor, command));
        service = Worker();
        var reply = await Send(actor, command);
        Assert.That(reply.status, Is.EqualTo(200), reply.body);
        var after = await StoredRoom();
        Assert.That(after.state.version, Is.EqualTo(before.state.version + 1));
        Assert.That(after.events.Take(before.events.Count).Select(Json.Encode),
            Is.EqualTo(before.events.Select(Json.Encode)));
        Assert.That(after.events, Has.Count.EqualTo(before.events.Count + 1));
        Assert.That(after.receipts[command.commandId].reply.body, Is.EqualTo(reply.body));
        Assert.That(reply.body, Does.Not.Contain("snapshot").And.Not.Contain("players").And.Not.Contain("board"));
        return reply;
    }

    private async Task OrdinaryReject(string actor, OnlineCommand command, string code)
    {
        string before = Json.Encode((await StoredRoom()).state);
        var reply = await Send(actor, command);
        Assert.That(reply.status, Is.EqualTo(409));
        Assert.That(Json.Decode<CommandReceipt>(reply.body).code, Is.EqualTo(code));
        Assert.That(Json.Encode((await StoredRoom()).state), Is.EqualTo(before));
    }

    private async Task AssertOrdinaryDecision(string kind, string owner, string waiting)
    {
        service = Worker();
        var state = (await StoredRoom()).state;
        Assert.That(state.pending.kind, Is.EqualTo(kind));
        Assert.That((await View(owner)).decision.id, Is.EqualTo(state.pending.id));
        var other = await View(waiting);
        Assert.That(other.decision, Is.Null);
        Assert.That(other.choices, Is.Empty);
        Assert.That(other.phase, Is.EqualTo("Waiting"));
    }
}
