using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    private void AlmostWon(int seat, VirtueType missing)
    {
        state.seats[seat].virtues = Definitions.Goals(state.seats[seat].kingdom).SelectMany(goal =>
            Enumerable.Range(0, goal.Value - (goal.Key == missing ? 1 : 0)).Select(_ =>
                new TokenState { id = Guid.NewGuid().ToString(), type = goal.Key })).ToList();
        Snapshot();
    }

    [Test]
    public void FinalChainRecipeWinsAndFinishedMatchHasNoChoicesOrCommands()
    {
        Board((0, ResourceType.Fire), (1, ResourceType.Air), (2, ResourceType.Water),
            (16, ResourceType.Water), (20, ResourceType.Earth));
        AlmostWon(0, VirtueType.Economy);
        Apply(0, "forgeChain", slots: new[] { 0, 1, 2 });
        Assert.That(state.phase, Is.EqualTo("Finished"));
        Assert.That(state.winner, Is.Zero);
        Assert.That(state.snapshot, Is.Null);
        foreach (int seat in new[] { 0, 1 })
        {
            Assert.That(Choices(seat), Is.Empty);
            Assert.That(Projection.ForSeat(state, seat).decision, Is.Null);
            foreach (string kind in new[] { "draw", "forgeChain", "weapon", "trade", "tradeAccept", "pass", "ack" })
                Reject(seat, Command(kind), "match_finished");
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CrossBoardChainCanWinOffTurnAndSimultaneousGoalsPreferActiveSeat(bool both)
    {
        Board((3, ResourceType.Water), (4, ResourceType.Earth), (5, ResourceType.Fire),
            (16, ResourceType.Water), (20, ResourceType.Earth));
        AlmostWon(1, VirtueType.Security);
        if (both) AlmostWon(0, VirtueType.Nature);
        Apply(0, "forgeChain", slots: new[] { 3, 4, 5 });
        Assert.That(state.winner, Is.EqualTo(-1));
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Apply(1, "pass");
        Assert.That(state.phase, Is.EqualTo("Finished"));
        Assert.That(state.winner, Is.EqualTo(both ? 0 : 1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void ChainVictoryWaitsForPassAndUseRestoresAllRewardsBeforeWinCheck(bool use)
    {
        Board((3, ResourceType.Water), (4, ResourceType.Earth), (5, ResourceType.Fire),
            (16, ResourceType.Water), (20, ResourceType.Earth));
        AlmostWon(0, VirtueType.Nature);
        Payment();
        string before = Encode(state.board);
        string activeInventory = Encode(state.seats[0].virtues);
        Apply(0, "forgeChain", slots: new[] { 3, 4, 5 });
        Assert.That(state.phase, Is.EqualTo("Decision"));
        string newlyEarned = state.seats[1].virtues.First(token => !state.snapshot.seats[1].virtues.Any(old => old.id == token.id)).id;
        var invalid = Command("use");
        invalid.paymentIds = new[] { "paid-art", newlyEarned };
        Reject(1, invalid, "invalid_payment");
        var command = Command(use ? "use" : "pass");
        command.paymentIds = use ? new[] { "paid-art", "paid-security" } : Array.Empty<string>();
        Apply(1, command);
        if (use)
        {
            Assert.That(Encode(state.board), Is.EqualTo(before));
            Assert.That(Encode(state.seats[0].virtues), Is.EqualTo(activeInventory));
            Assert.That(state.seats[1].virtues.Select(token => token.id), Is.EqualTo(new[] { "unpaid-nature" }));
            Apply(1, "ack");
            Assert.That(state.winner, Is.EqualTo(-1));
            Assert.That(state.phase, Is.EqualTo("Action"));
            Assert.That(state.activeSeat, Is.EqualTo(1));
        }
        else
        {
            Assert.That(state.phase, Is.EqualTo("Finished"));
            Assert.That(state.winner, Is.Zero);
        }
    }
}
