using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    [Test]
    public void WeaponAcceptsConnectedSetNotJustOrderedPathAndOpponentChoosesOwnDiscard()
    {
        Board((3, ResourceType.Fire), (4, ResourceType.Fire), (12, ResourceType.Fire),
            (16, ResourceType.Water), (20, ResourceType.Earth), (28, ResourceType.Air));
        Assert.That(OrdinarySelection.Error(state.board, 0, new[] { 3, 12, 4 }, true), Is.Null);
        Assert.That(Choices(0), Does.Contain("weapon"));
        string snapshot = state.snapshot.actionId;
        Apply(0, "weapon", slots: new[] { 3, 12, 4 });
        Assert.That(state.pending.kind, Is.EqualTo("WeaponDiscard"));
        Assert.That(state.pending.owner, Is.EqualTo(1));
        Assert.That(state.pending.continuation, Is.EqualTo("AfterAction"));
        Assert.That(state.snapshot.actionId, Is.EqualTo(snapshot));
        Assert.That(state.board.Count(slot => slot.pieceId != null), Is.EqualTo(3));
        Assert.That(Projection.ForSeat(state, 1).choices.Select(choice => choice.to), Is.EquivalentTo(new[] { 20, 28 }));
        Assert.That(Projection.ForSeat(state, 0).decision, Is.Null);
        Assert.That(Choices(0), Is.Empty);
        Reject(0, Command("weaponDiscard", to: 20), "stale_decision");
        Reject(1, Command("weaponDiscard", to: 16), "wrong_owner");
        Reject(1, Command("weaponDiscard", to: 4), "missing_resource");
        var stale = Command("weaponDiscard", to: 20);
        stale.decisionId = "old";
        Reject(1, stale, "stale_decision");
        Apply(1, "weaponDiscard", to: 20);
        Assert.That(state.board[20].pieceId, Is.Null);
        Assert.That(state.board[28].pieceId, Is.EqualTo("piece-28"));
        Assert.That(state.phase, Is.EqualTo("Action"));
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [TestCase(new[] { 0, 1 })]
    [TestCase(new[] { 0, 1, 2, 3 })]
    [TestCase(new[] { 0, 1, 0 })]
    [TestCase(new[] { 0, 1, 9 })]
    [TestCase(new[] { 0, 1, 16 })]
    [TestCase(new[] { 0, 1, 31 })]
    [TestCase(new[] { 4, 0, 1 })]
    [TestCase(new[] { 0, 1, -1 })]
    public void InvalidWeaponSelectionsNeverMutateState(int[] slots)
    {
        Board((0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire), (3, ResourceType.Fire),
            (4, ResourceType.Fire), (9, ResourceType.Water), (16, ResourceType.Fire));
        Reject(0, Command("weapon", slots: slots), "invalid_weapon");
    }

    [Test]
    public void WeaponWithNoRemainingOpposingResourceSkipsOnlyTheDiscard()
    {
        Board((3, ResourceType.Water), (4, ResourceType.Water), (12, ResourceType.Water), (16, ResourceType.Fire));
        Payment();
        Apply(0, "weapon", slots: new[] { 3, 4, 12 });
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Assert.That(state.pending.owner, Is.EqualTo(1));
        Apply(1, "pass");
        Assert.That(state.phase, Is.EqualTo("Action"));
        Assert.That(state.activeSeat, Is.Zero, "The existing empty-board turn skip is preserved.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WeaponAndDiscardResolveAsOneRetractableAction(bool use)
    {
        Board((0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire),
            (16, ResourceType.Water), (20, ResourceType.Earth), (28, ResourceType.Air));
        Payment();
        string before = Encode(state.board);
        string deck = Encode(state.deck);
        Apply(0, "weapon", slots: new[] { 1, 0, 2 });
        Assert.That(state.pending.kind, Is.EqualTo("WeaponDiscard"));
        Reject(1, Command("pass"), "stale_decision");
        Apply(1, "weaponDiscard", to: 20);
        string after = Encode(state.board);
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        var command = Command(use ? "use" : "pass");
        command.paymentIds = use ? new[] { "paid-art", "paid-security" } : Array.Empty<string>();
        Apply(1, command);
        Assert.That(Encode(state.board), Is.EqualTo(use ? before : after));
        Assert.That(Encode(state.deck), Is.EqualTo(deck));
        Assert.That(state.seats[1].virtues.Select(token => token.id), Is.EquivalentTo(use
            ? new[] { "unpaid-nature" } : new[] { "paid-art", "paid-security", "unpaid-nature" }));
        if (use)
        {
            Assert.That(state.reactionPayments.Single().tokens.Select(token => token.id),
                Is.EquivalentTo(new[] { "paid-art", "paid-security" }));
            Assert.That(state.seats[1].revealed, Is.True);
            Apply(1, "ack");
        }
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Assert.That(state.phase, Is.EqualTo("Action"));
    }
}
