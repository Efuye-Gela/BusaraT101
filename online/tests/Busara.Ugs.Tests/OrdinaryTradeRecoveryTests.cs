using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task OrdinaryTradeSurvivesWorkersLostAcksAndExactRetraction(bool use)
    {
        await EarnOrdinaryPayment();
        string before = Json.Encode((await StoredRoom()).state.board);
        var offer = await Make("host", "trade");
        offer.from = 0; offer.resourceType = 2;
        var offered = await OrdinaryLostAck("host", offer);
        await AssertOrdinaryDecision("TradeResponse", "guest", "host");
        var wrong = await Make("host", "tradeAccept");
        wrong.decisionId = (await StoredRoom()).state.pending.id;
        await OrdinaryReject("host", wrong, "stale_decision");
        var stale = await Make("guest", "tradeAccept");
        stale.expectedVersion = offer.expectedVersion;
        await OrdinaryReject("guest", stale, "stale_version");
        await OrdinaryLostAck("guest", await Make("guest", "tradeAccept"));
        await AssertOrdinaryDecision("TradeSelect", "host", "guest");
        Assert.That((await View("host")).choices.Count(choice => choice.kind == "tradeComplete"), Is.EqualTo(1));
        var complete = await Make("host", "tradeComplete");
        complete.to = 20;
        await OrdinaryLostAck("host", complete);
        await AssertOrdinaryDecision("Retraction", "guest", "host");
        string after = Json.Encode((await StoredRoom()).state.board);
        string stable = Json.Encode(await StoredRoom());
        Assert.That((await Send("host", offer)).body, Is.EqualTo(offered.body), "Duplicate lookup precedes stale version.");
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(stable));
        string[] payment = (await StoredRoom()).state.seats[1].virtues.Select(token => token.id).ToArray();
        var reaction = await Make("guest", use ? "use" : "pass");
        reaction.paymentIds = use ? payment : Array.Empty<string>();
        await OrdinaryLostAck("guest", reaction);
        var restored = (await StoredRoom()).state;
        Assert.That(Json.Encode(restored.board), Is.EqualTo(use ? before : after));
        Assert.That(restored.seats[1].virtues.Count, Is.EqualTo(use ? 0 : 2));
        if (use)
        {
            Assert.That(restored.reactionPayments.Single().tokens.Select(token => token.id), Is.EquivalentTo(payment));
            await Apply("guest", "ack");
        }
        Assert.That((await StoredRoom()).state.activeSeat, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task OrdinaryTradeCancelSurvivesRestartWithoutReplacingActionSnapshot(bool accept)
    {
        await EarnOrdinaryPayment();
        var before = (await StoredRoom()).state;
        var offer = await Make("host", "trade");
        offer.from = 0; offer.resourceType = 2;
        await OrdinaryLostAck("host", offer);
        await OrdinaryLostAck("guest", await Make("guest", accept ? "tradeAccept" : "tradeReject"));
        await AssertOrdinaryDecision(accept ? "TradeSelect" : "TradeDeclined", "host", "guest");
        var cancel = await Make("host", "tradeCancel");
        var receipt = await OrdinaryLostAck("host", cancel);
        var after = (await StoredRoom()).state;
        Assert.That(Json.Encode(after.board), Is.EqualTo(Json.Encode(before.board)));
        Assert.That(Json.Encode(after.seats), Is.EqualTo(Json.Encode(before.seats)));
        Assert.That(Json.Encode(after.snapshot), Is.EqualTo(Json.Encode(before.snapshot)));
        Assert.That(after.phase, Is.EqualTo("Action"));
        Assert.That(after.activeSeat, Is.Zero);
        await Apply("host", "move", 24, 25);
        string stable = Json.Encode(await StoredRoom());
        Assert.That((await Send("host", cancel)).body, Is.EqualTo(receipt.body));
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(stable));
    }

    [Test]
    public async Task OrdinaryTradeConcurrentAcceptAndRejectHaveOneDurableOutcome()
    {
        await EarnOrdinaryPayment();
        await Apply("host", "trade", from: 0, type: 2);
        var accept = await Make("guest", "tradeAccept");
        var reject = await Make("guest", "tradeReject");
        var before = await StoredRoom();
        store.RaceNextTwoReads("busara_" + match);
        var replies = await Task.WhenAll(
            Worker().Execute("guest", "command", Json.Encode(accept), match),
            Worker().Execute("guest", "command", Json.Encode(reject), match));
        Assert.That(replies.Select(reply => reply.status), Is.EquivalentTo(new[] { 200, 409 }));
        var after = await StoredRoom();
        Assert.That(after.state.version, Is.EqualTo(before.state.version + 1));
        Assert.That(after.events, Has.Count.EqualTo(before.events.Count + 1));
        Assert.That(after.state.pending.kind, Is.AnyOf("TradeSelect", "TradeDeclined"));
        Assert.That(Json.Encode(after.state.board), Is.EqualTo(Json.Encode(before.state.board)));
        Assert.That(store.Conflicts, Is.GreaterThanOrEqualTo(1));
    }
}
