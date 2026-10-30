using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class FullPowerTests
{
    [Test]
    public void NecklaceCancelsTheEffectButBothCostsStayPaid()
    {
        Setup("konga", "bis-bese-avouman", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art);
        Give(1, VirtueType.Nature, VirtueType.Nature, VirtueType.Nature);
        Apply(0, "power", payment: Ids(0, 2));
        Assert.That(state.pending.kind, Is.EqualTo("Reaction"));
        Assert.That(state.pending.window, Is.EqualTo(PowerWindow.Necklace));
        Assert.That(View(1).decision.prompt, Does.Contain("Magic"));
        Reject(0, Command("use", payment: Ids(0, 0)), "stale_decision");
        Apply(1, "use", payment: Ids(1, 3));
        Assert.That(state.seats.All(seat => seat.virtues.Count == 0), Is.True);
        Assert.That(state.extraActions, Is.Zero);
        Assert.That(state.notice, Does.Contain("cancelled by King's Necklace"));
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void PassingNecklaceLetsThePowerResolve()
    {
        Setup("konga", "bis-bese-avouman", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art);
        Give(1, VirtueType.Nature, VirtueType.Nature, VirtueType.Nature);
        Apply(0, "power", payment: Ids(0, 2));
        Apply(1, "pass");
        Assert.That(state.extraActions, Is.EqualTo(1));
        Assert.That(state.seats[1].virtues.Count, Is.EqualTo(3));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DomeIsOfferedOnlyForWeaponsAndPreventsTheDiscard(bool use)
    {
        Setup("konga", "empire-volta", (0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire),
            (20, ResourceType.Earth));
        Give(1, VirtueType.Energy);
        Apply(0, "weapon", slots: new[] { 0, 1, 2 });
        Assert.That(state.pending.window, Is.EqualTo(PowerWindow.Dome));
        Apply(1, use ? "use" : "pass", payment: use ? Ids(1, 1) : null);
        if (!use)
        {
            Assert.That(state.pending.kind, Is.EqualTo("WeaponDiscard"));
            Apply(1, "weaponDiscard", to: 20);
        }
        Assert.That(state.board[20].pieceId == null, Is.EqualTo(!use));
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void RetractionRestoresTheActionRefundsLedgeredCostsAndEndsTheTurn()
    {
        Setup("konga", "mask-of-light", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art);
        Give(1, VirtueType.Art, VirtueType.Security);
        string before = Encode(state.board);
        Apply(0, "power", payment: Ids(0, 2));
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        int version = (int)state.version;
        Apply(1, "use", payment: Ids(1, 2));
        Assert.That(state.pending.kind, Is.EqualTo("RetractionNotice"));
        Assert.That(state.seats[0].virtues.Count, Is.EqualTo(2), "Own-turn power payment is restored.");
        Assert.That(state.seats[1].virtues, Is.Empty, "The Retraction payment remains spent.");
        Assert.That(state.version, Is.GreaterThan(version));
        Assert.That(Encode(state.board), Is.EqualTo(before));
        Apply(1, "ack");
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Assert.That(state.extraActions, Is.Zero);
    }

    [Test]
    public void ManipulationLetsTheControllerChooseTheActionButNotTrade()
    {
        Setup("konga", "royaume-kongo", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(1, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        state.activeSeat = 1;
        state.snapshot = DomainRules.Capture(state);
        Apply(1, "move", from: 4, to: 5);
        Assert.That(state.pending.window, Is.EqualTo(PowerWindow.TurnStart));
        Apply(1, "use", payment: Ids(1, 3));
        Assert.That(state.controller, Is.EqualTo(1));
        Assert.That(state.activeSeat, Is.Zero);
        Assert.That(Choices(0), Is.Empty);
        Assert.That(Choices(1), Does.Contain("move").And.Not.Contain("trade"));
        Reject(0, Command("move", from: 0, to: 1), "controlled_turn");
        Reject(1, Command("trade", from: 0, type: 1));
        Apply(1, "move", from: 0, to: 1);
        Assert.That(state.board[1].type, Is.EqualTo(ResourceType.Water));
        Assert.That(state.controller, Is.EqualTo(-1));
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }

    [Test]
    public void TimeTakesAnExtraTurnAndThenResumesOrder()
    {
        Setup("konga", "telalila", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(1, VirtueType.Art, VirtueType.Art);
        Apply(0, "move", from: 0, to: 1);
        Assert.That(state.pending.window, Is.EqualTo(PowerWindow.AfterAction));
        Apply(1, "use", payment: Ids(1, 2));
        Assert.That(state.activeSeat, Is.EqualTo(1));
        Apply(1, "move", from: 4, to: 5);
        Assert.That(state.activeSeat, Is.EqualTo(1), "The extra turn follows the normal next turn.");
        Apply(1, "move", from: 5, to: 6);
        Assert.That(state.activeSeat, Is.Zero);
    }

    [Test]
    public void RainAddsForEveryPlayerAndSkipsWhenNoSpaceRemains()
    {
        Setup("kavango-keendobe", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 3), count: -1);
        Assert.That(state.pending.kind, Is.EqualTo("RemoveResource"));
        Apply(0, "removeResource", to: 0);
        Assert.That(state.pending.owner, Is.EqualTo(1));
        Assert.That(Choices(0), Is.Empty);
        Apply(1, "removeResource", to: 4);
        Assert.That(state.board.All(slot => slot.pieceId == null), Is.True);
    }

    [Test]
    public void BlessingCopiesOnlyTheCastersBoardTypes()
    {
        Setup("logone", "konga", (0, ResourceType.Water), (1, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 4));
        Assert.That(state.pending.remaining, Is.EqualTo(2));
        Assert.That(View(0).choices.Select(choice => choice.resourceType).Distinct(), Is.EqualTo(new[] { (int)ResourceType.Water }));
        Reject(0, Command("addResource", to: 2, type: (int)ResourceType.Fire), "no_stock");
        Apply(0, "addResource", to: 2, type: (int)ResourceType.Water);
        Apply(0, "addResource", to: 3, type: (int)ResourceType.Water);
        Assert.That(state.activeSeat, Is.EqualTo(1));
    }
}
