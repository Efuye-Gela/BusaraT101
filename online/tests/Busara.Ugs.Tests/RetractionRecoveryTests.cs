using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
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
        await SaveLegacyRoom();
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
}
