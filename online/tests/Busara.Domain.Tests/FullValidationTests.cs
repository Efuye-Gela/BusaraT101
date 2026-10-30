using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class FullPowerTests
{
    [Test]
    public void FullMatchesDealDistinctCatalogKingdoms()
    {
        var match = DomainRules.Join(DomainRules.Create("deal"), 1, "Bo");
        foreach (int seat in new[] { 0, 1 })
            match = DomainRules.Apply(match, seat, new OnlineCommand
            {
                commandId = Guid.NewGuid().ToString(), expectedVersion = match.version.ToString(), kind = "configure",
                name = seat == 0 ? "Ada" : "Bo", ready = true
            }, new FixedRandom()).state;
        match = DomainRules.Apply(match, 0, new OnlineCommand
        {
            commandId = Guid.NewGuid().ToString(), expectedVersion = match.version.ToString(), kind = "start"
        }, new FixedRandom()).state;
        Assert.That(match.seats.Select(seat => seat.kingdom).Distinct().Count(), Is.EqualTo(2));
        Assert.That(match.seats.All(seat => KingdomCatalog.Exists(seat.kingdom)), Is.True);
    }

    [Test]
    public void CorruptPowerStateIsRejected()
    {
        Setup("milu", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art);
        Apply(0, "power", payment: Ids(0, 2));
        Assert.That(state.pending.kind, Is.EqualTo("Steal"));
        foreach (Action<MatchState> corrupt in new Action<MatchState>[]
        {
            value => value.power = null,
            value => value.pending.owner = 1,
            value => value.pending.continuation = "AfterAction",
            value => value.power.caster = 1,
            value => value.controller = 0,
            value => value.extraTurns.Add(4),
            value => value.ruleset = Definitions.OrdinaryRuleset
        })
        {
            MatchState invalid = DomainRules.Clone(state);
            corrupt(invalid);
            Assert.That(() => DomainRules.ValidateState(invalid), Throws.TypeOf<RuleException>());
        }
    }

    [Test]
    public void StaleAndForeignPowerCommandsChangeNothing()
    {
        Setup("logone", "konga", (0, ResourceType.Water), (4, ResourceType.Fire));
        Give(0, VirtueType.Art, VirtueType.Art, VirtueType.Art, VirtueType.Art);
        Give(1, VirtueType.Art, VirtueType.Art);
        Reject(0, Command("power", payment: Ids(1, 2).Concat(Ids(0, 2)).ToArray()), "invalid_payment");
        Reject(1, Command("power", payment: Ids(1, 2)), "wrong_phase");
        Reject(0, Command("power", payment: Ids(0, 4), count: 2), "invalid_power");
        Apply(0, "power", payment: Ids(0, 4));
        OnlineCommand stale = Command("addResource", to: 1, type: 0);
        stale.decisionId = "old";
        Reject(0, stale, "stale_decision");
    }
}
