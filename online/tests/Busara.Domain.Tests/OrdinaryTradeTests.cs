using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    [Test]
    public void TradeRequiresOwnedOfferAndDifferentTypeButUnavailableRequestsCanBeRejected()
    {
        var offers = Projection.ForSeat(state, 0).choices.Where(choice => choice.kind == "trade" && choice.from == 0);
        Assert.That(offers.Select(choice => choice.resourceType), Is.EquivalentTo(new[] { 1, 2, 3 }));
        Reject(0, Command("trade", from: 4, type: 0), "wrong_owner");
        Reject(0, Command("trade", from: 0, type: 0), "invalid_trade");
        Reject(0, Command("trade", from: 0, type: 4), "invalid_resource");
        Apply(0, "trade", from: 0, type: 3);
        Assert.That(Choices(1), Is.EqualTo(new[] { "tradeReject" }));
        Reject(1, Command("tradeAccept"), "invalid_trade");
        Apply(1, "tradeReject");
        Assert.That(Choices(0), Is.EqualTo(new[] { "tradeCancel" }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AcceptedOrDeclinedTradeCanReturnToSameActionWithoutSpendingTurn(bool accept)
    {
        string board = Encode(state.board);
        string seats = Encode(state.seats);
        string snapshot = Encode(state.snapshot);
        Apply(0, "trade", from: 0, type: 2);
        Assert.That(state.pending.kind, Is.EqualTo("TradeResponse"));
        Reject(0, Command("tradeCancel"), "stale_decision");
        Reject(0, Command("tradeAccept"), "stale_decision");
        Apply(1, accept ? "tradeAccept" : "tradeReject");
        Assert.That(state.pending.kind, Is.EqualTo(accept ? "TradeSelect" : "TradeDeclined"));
        Assert.That(state.pending.owner, Is.Zero);
        Reject(1, Command("tradeCancel"), "stale_decision");
        Apply(0, "tradeCancel");
        Assert.That(state.pending, Is.Null);
        Assert.That(state.phase, Is.EqualTo("Action"));
        Assert.That(state.activeSeat, Is.Zero);
        Assert.That(Encode(state.board), Is.EqualTo(board));
        Assert.That(Encode(state.seats), Is.EqualTo(seats));
        Assert.That(Encode(state.snapshot), Is.EqualTo(snapshot));
        Apply(0, "move", from: 0, to: 1);
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void TradeRequiresExplicitExactConfirmationEvenWhenOnlyOneTargetMatches()
    {
        string board = Encode(state.board);
        string offered = state.board[0].pieceId;
        string received = state.board[4].pieceId;
        Apply(0, "trade", from: 0, type: 2);
        Apply(1, "tradeAccept");
        Assert.That(state.pending.kind, Is.EqualTo("TradeSelect"));
        Assert.That(Encode(state.board), Is.EqualTo(board));
        Assert.That(Projection.ForSeat(state, 0).choices.Where(choice => choice.kind == "tradeComplete")
            .Select(choice => choice.to), Is.EqualTo(new[] { 4 }));
        Reject(1, Command("tradeComplete", to: 4), "stale_decision");
        Reject(0, Command("tradeComplete", to: 20), "invalid_trade");
        Reject(0, Command("tradeComplete", to: 16), "wrong_owner");
        Apply(0, "tradeComplete", to: 4);
        Assert.That(state.board[0].pieceId, Is.EqualTo(received));
        Assert.That(state.board[0].type, Is.EqualTo(ResourceType.Fire));
        Assert.That(state.board[4].pieceId, Is.EqualTo(offered));
        Assert.That(state.board[4].type, Is.EqualTo(ResourceType.Water));
        Assert.That(state.seats.All(seat => seat.virtues.Count == 0), Is.True);
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void TradeSelectListsEveryMatchingResourceWithoutExposingPrivateState()
    {
        Board((0, ResourceType.Water), (4, ResourceType.Fire), (5, ResourceType.Fire), (16, ResourceType.Earth));
        Payment();
        state.seats[1].virtuesHidden = true;
        Snapshot();
        Apply(0, "trade", from: 0, type: 2);
        Assert.That(Projection.ForSeat(state, 0).decision, Is.Null);
        Assert.That(Projection.ForSeat(state, 0).choices, Is.Empty);
        Assert.That(Projection.ForSeat(state, 1).decision.prompt, Does.Contain("Water #0").And.Contain("Fire"));
        Apply(1, "tradeAccept");
        var owner = Projection.ForSeat(state, 0);
        Assert.That(owner.choices.Where(choice => choice.kind == "tradeComplete").Select(choice => choice.to),
            Is.EquivalentTo(new[] { 4, 5 }));
        Assert.That(owner.players[1].kingdom, Is.Null);
        Assert.That(owner.players[1].virtues, Is.Empty);
        Assert.That(Encode(owner), Does.Not.Contain("paid-art").And.Not.Contain("snapshot").And.Not.Contain("deck"));
        Assert.That(Projection.ForSeat(state, 1).decision, Is.Null);
        Assert.That(Choices(1), Is.Empty);
        Apply(0, "tradeComplete", to: 5);
        Assert.That(state.board[4].pieceId, Is.EqualTo("piece-4"));
        Assert.That(state.board[0].pieceId, Is.EqualTo("piece-5"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CompletedTradeUsesNormalRetractionAndExactSnapshotPayment(bool use)
    {
        Payment();
        string before = Encode(state.board);
        Apply(0, "trade", from: 0, type: 2);
        Apply(1, "tradeAccept");
        Apply(0, "tradeComplete", to: 4);
        string after = Encode(state.board);
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        var command = Command(use ? "use" : "pass");
        command.paymentIds = use ? new[] { "paid-art", "paid-security" } : Array.Empty<string>();
        Apply(1, command);
        Assert.That(Encode(state.board), Is.EqualTo(use ? before : after));
        Assert.That(state.seats[1].virtues, Has.Count.EqualTo(use ? 1 : 3));
        if (use) Apply(1, "ack");
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void TradeDecisionRejectsStaleVersionAndDecisionBeforeChangingBoard()
    {
        Apply(0, "trade", from: 0, type: 2);
        var command = Command("tradeAccept");
        command.expectedVersion = (state.version - 1).ToString();
        Reject(1, command, "stale_version");
        command.expectedVersion = state.version.ToString();
        command.decisionId = "old";
        Reject(1, command, "stale_decision");
    }
}
