using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class FullPowerTests
{
    [Test]
    public void AbundanceIsFreeHiddenOnlyAndAddsTwoChosenResources()
    {
        Setup("egolica", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Assert.That(Choices(0), Does.Contain("power"));
        Apply(0, "power");
        Assert.That(state.seats[0].revealed, Is.True);
        Assert.That(state.pending.kind, Is.EqualTo("AddResource"));
        Assert.That(View(1).decision, Is.Null);
        Reject(1, Command("addResource", to: 1, type: 0), "stale_decision");
        Apply(0, "addResource", to: 1, type: (int)ResourceType.Earth);
        Assert.That(state.pending.remaining, Is.EqualTo(1));
        Apply(0, "addResource", to: 2, type: (int)ResourceType.Air);
        Assert.That(state.board[2].type, Is.EqualTo(ResourceType.Air));
        Assert.That(state.activeSeat, Is.EqualTo(1));
        state.activeSeat = 0;
        Assert.That(DomainRules.CanUse(state, 0), Is.False, "Abundance is hidden-only.");
    }

    [Test]
    public void MagicGrantsTwoMoreActionsAndCostIsExact()
    {
        Setup("konga", "aradas", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Wisdom, VirtueType.Nature);
        Reject(0, Command("power", payment: Ids(0, 1)), "invalid_payment");
        Apply(0, "power", payment: Ids(0, 2));
        Assert.That(state.seats[0].virtues.Select(token => token.type), Is.EqualTo(new[] { VirtueType.Nature }));
        Assert.That(state.activeSeat, Is.Zero);
        Assert.That(state.extraActions, Is.EqualTo(1));
        Apply(0, "move", from: 0, to: 1);
        Assert.That(state.activeSeat, Is.Zero);
        Apply(0, "move", from: 1, to: 2);
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void KnowledgeIsPrivateToTheCaster()
    {
        Setup("nevulandis", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 1));
        Assert.That(View(0).decision.prompt, Does.Contain("Konga"));
        Assert.That(View(1).decision, Is.Null);
        Assert.That(state.seats[1].knownKingdoms, Is.Empty);
        Apply(0, "ack");
        Assert.That(View(0).players[1].kingdom, Does.StartWith("Konga"));
        Assert.That(View(1).players[0].kingdom, Does.StartWith("N'evulandis"), "Using a power reveals the caster.");
    }

    [Test]
    public void IdentitySurfingSwapsKingdomsAndRevealState()
    {
        Setup("aradas", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 3));
        Assert.That(state.seats[0].kingdom, Is.EqualTo("konga"));
        Assert.That(state.seats[1].kingdom, Is.EqualTo("aradas"));
        Assert.That(state.seats[1].revealed, Is.True);
        Assert.That(state.seats[0].revealed, Is.False);
    }

    [Test]
    public void TransformExchangesOnlySeparateOwnedVirtuesWithinStock()
    {
        Setup("eko-akete", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art, VirtueType.Art, VirtueType.Nature, VirtueType.Energy);
        string[] payment = Ids(0, 4);
        string[] exchange = state.seats[0].virtues.Skip(4).Select(token => token.id).ToArray();
        Reject(0, Command("power", payment: payment, virtue: 2, exchange: payment.Take(1).ToArray()), "invalid_power");
        Apply(0, "power", payment: payment, virtue: (int)VirtueType.Wisdom, exchange: exchange);
        Assert.That(state.seats[0].virtues.Select(token => token.type), Is.EqualTo(new[] { VirtueType.Wisdom, VirtueType.Wisdom }));
    }

    [Test]
    public void ImaginationTakesAVisibleVirtueAndHiddenMissesDoNotLeakOrCreateTokens()
    {
        Setup("ubunifu", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Give(1, VirtueType.Economy);
        Assert.That(View(0).choices.Where(choice => choice.kind == "power").Select(choice => choice.virtueType),
            Is.EqualTo(new[] { (int)VirtueType.Economy }));
        Reject(0, Command("power", payment: Ids(0, 3), virtue: (int)VirtueType.Art), "invalid_power");
        state.seats[1].virtuesHidden = true;
        Assert.That(View(0).choices.Count(choice => choice.kind == "power"), Is.EqualTo(6));
        Apply(0, "power", payment: Ids(0, 3), virtue: (int)VirtueType.Art);
        Assert.That(state.seats[0].virtues, Is.Empty);
        Assert.That(state.seats[1].virtues.Count, Is.EqualTo(1));
        Assert.That(state.notice, Does.Contain("found no Art"));
    }

    [Test]
    public void InvisibilityStealsIntoAnEmptySpaceAndHidesVirtues()
    {
        Setup("milu", "konga", (0, ResourceType.Water), (4, ResourceType.Fire), (5, ResourceType.Earth));
        Give(0, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 2));
        Assert.That(state.pending.kind, Is.EqualTo("Steal"));
        Reject(0, Command("steal", from: 0, to: 1), "wrong_owner");
        Apply(0, "steal", from: 5, to: 1);
        Assert.That(state.board[1].type, Is.EqualTo(ResourceType.Earth));
        Assert.That(state.board[5].pieceId, Is.Null);
        Assert.That(state.seats[0].virtuesHidden, Is.True);
    }

    [Test]
    public void WitchcraftRearrangesWithinOwnKingdomUntilFinished()
    {
        Setup("ilagik", "konga", (0, ResourceType.Water), (1, ResourceType.Fire), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 3));
        string first = state.pending.id;
        Reject(0, Command("witchMove", from: 0, to: 4), "invalid_move");
        Apply(0, "witchMove", from: 0, to: 1);
        Assert.That(state.pending.id, Is.Not.EqualTo(first));
        Assert.That(state.board[0].type, Is.EqualTo(ResourceType.Fire));
        Assert.That(state.board[1].type, Is.EqualTo(ResourceType.Water));
        Apply(0, "witchFinish");
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }
}
