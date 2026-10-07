using Busara.Online;
using NUnit.Framework;

namespace Busara.Ugs.Tests;

public sealed partial class MatchServiceTests
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task OrdinaryWeaponDiscardAndRetractionSurviveWorkersAndLostAcks(bool use)
    {
        await EarnOrdinaryPayment();
        await Apply("host", "draw");
        await Apply("host", "place", to: 1);
        await Apply("guest", "pass");
        await Apply("guest", "move", 20, 21);
        var before = (await StoredRoom()).state;
        string[] payment = before.seats[1].virtues.Select(token => token.id).ToArray();
        var weapon = await Make("host", "weapon");
        weapon.slots = new[] { 1, 2, 9 };
        var receipt = await OrdinaryLostAck("host", weapon);
        await AssertOrdinaryDecision("WeaponDiscard", "guest", "host");
        var wrong = await Make("host", "weaponDiscard");
        wrong.to = 21; wrong.decisionId = (await StoredRoom()).state.pending.id;
        await OrdinaryReject("host", wrong, "stale_decision");
        var wrongSlot = await Make("guest", "weaponDiscard");
        wrongSlot.to = 0;
        await OrdinaryReject("guest", wrongSlot, "wrong_owner");
        var stale = await Make("guest", "weaponDiscard");
        stale.to = 21; stale.decisionId = "old-decision";
        await OrdinaryReject("guest", stale, "stale_decision");
        var discard = await Make("guest", "weaponDiscard");
        discard.to = 21;
        await OrdinaryLostAck("guest", discard);
        await AssertOrdinaryDecision("Retraction", "guest", "host");
        string after = Json.Encode((await StoredRoom()).state.board);
        string stable = Json.Encode(await StoredRoom());
        Assert.That((await Send("host", weapon)).body, Is.EqualTo(receipt.body));
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(stable));
        weapon.slots = new[] { 1, 9, 2 };
        var conflict = await Send("host", weapon);
        Assert.That(conflict.status, Is.EqualTo(409));
        Assert.That(conflict.body, Does.Contain("command_conflict"));
        Assert.That(Json.Encode(await StoredRoom()), Is.EqualTo(stable));
        var reaction = await Make("guest", use ? "use" : "pass");
        reaction.paymentIds = use ? payment : Array.Empty<string>();
        await OrdinaryLostAck("guest", reaction);
        var result = (await StoredRoom()).state;
        Assert.That(Json.Encode(result.board), Is.EqualTo(use ? Json.Encode(before.board) : after));
        Assert.That(result.seats[1].virtues.Count, Is.EqualTo(use ? 0 : 2));
        Assert.That(Json.Encode(result.deck), Is.EqualTo(Json.Encode(before.deck)));
        if (use)
        {
            Assert.That(result.reactionPayments.Single().tokens.Select(token => token.id), Is.EquivalentTo(payment));
            await Apply("guest", "ack");
        }
        Assert.That((await StoredRoom()).state.activeSeat, Is.EqualTo(use ? 1 : 0));
    }
}
