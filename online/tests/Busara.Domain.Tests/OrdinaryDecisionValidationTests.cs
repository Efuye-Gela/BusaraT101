using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    [TestCase("TradeResponse")]
    [TestCase("TradeSelect")]
    [TestCase("TradeDeclined")]
    [TestCase("WeaponDiscard")]
    public void NewDecisionsCloneRoundTripAndRejectCorruptOwnerContinuationOrLegacyRules(string kind)
    {
        if (kind == "WeaponDiscard")
        {
            Board((0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire),
                (16, ResourceType.Water), (20, ResourceType.Earth));
            Apply(0, "weapon", slots: new[] { 0, 1, 2 });
        }
        else
        {
            Apply(0, "trade", from: 0, type: 2);
            if (kind != "TradeResponse") Apply(1, kind == "TradeSelect" ? "tradeAccept" : "tradeReject");
        }
        MatchState original = DomainRules.Clone(state);
        Assert.That(Encode(original), Is.EqualTo(Encode(state)));
        state = RoundTrip(original);
        Assert.That(state.pending.kind, Is.EqualTo(kind));
        foreach (Action<MatchState> corrupt in new Action<MatchState>[]
        {
            value => value.pending.owner = 1 - value.pending.owner,
            value => value.pending.continuation = "WrongContinuation",
            value => value.pending.id = "",
            value => value.pending.remaining = 1,
            value => value.pending.drawnCard = value.deck[0],
            value => value.ruleset = Definitions.LegacyRuleset
        })
        {
            var invalid = DomainRules.Clone(state);
            corrupt(invalid);
            Assert.That(() => DomainRules.ValidateState(invalid), Throws.TypeOf<RuleException>());
        }
        Assert.That(Encode(state), Is.EqualTo(Encode(original)));
    }

    [TestCase("TradeResponse")]
    [TestCase("TradeSelect")]
    [TestCase("TradeDeclined")]
    public void PersistedTradeRequiresValidOwnedOriginalOfferAndMatchingAcceptedTarget(string kind)
    {
        Apply(0, "trade", from: 0, type: 2);
        if (kind != "TradeResponse") Apply(1, kind == "TradeSelect" ? "tradeAccept" : "tradeReject");
        foreach (Action<MatchState> corrupt in new Action<MatchState>[]
        {
            value => value.pending.offerFrom = 32,
            value => value.pending.offerFrom = 4,
            value => value.pending.offerFrom = 1,
            value => value.pending.offerResourceType = -1,
            value => value.pending.offerResourceType = 4,
            value => value.pending.offerResourceType = 0,
            value => value.snapshot.board[0].pieceId = "changed-source"
        })
        {
            var invalid = DomainRules.Clone(state);
            corrupt(invalid);
            Assert.That(() => DomainRules.ValidateState(invalid), Throws.TypeOf<RuleException>());
        }
        if (kind == "TradeSelect")
        {
            state.board[4].pieceId = null;
            Assert.That(() => DomainRules.ValidateState(state), Throws.TypeOf<RuleException>());
        }
    }

    [Test]
    public void PersistedWeaponDiscardRequiresEligibleTargetAndNoTradeFields()
    {
        Board((0, ResourceType.Fire), (1, ResourceType.Fire), (2, ResourceType.Fire), (20, ResourceType.Earth));
        Apply(0, "weapon", slots: new[] { 0, 1, 2 });
        var invalid = DomainRules.Clone(state);
        invalid.pending.offerFrom = 0;
        Assert.That(() => DomainRules.ValidateState(invalid), Throws.TypeOf<RuleException>());
        state.board[20].pieceId = null;
        Assert.That(() => DomainRules.ValidateState(state), Throws.TypeOf<RuleException>());
    }
}
