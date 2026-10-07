using Busara.Online;
using NUnit.Framework;

namespace Busara.Domain.Tests;

public sealed partial class OrdinaryActionTests
{
    [Test]
    public void NewMatchesUseV2WhilePersistedV1RetainsPairOnlyRules()
    {
        Assert.That(new MatchState().ruleset, Is.EqualTo("busara-online-v3"));
        Assert.That(DomainRules.Create("new").ruleset, Is.EqualTo(Definitions.CurrentRuleset));
        Board((3, ResourceType.Water), (4, ResourceType.Fire), (5, ResourceType.Fire),
            (6, ResourceType.Fire), (16, ResourceType.Earth), (20, ResourceType.Earth));
        state.ruleset = Definitions.LegacyRuleset;
        state = RoundTrip(state);
        Assert.That(state.schemaVersion, Is.EqualTo(1));
        Assert.That(Choices(0), Does.Contain("forge").And.Not.Contain("forgeChain").And.Not.Contain("weapon").And.Not.Contain("trade"));
        foreach (string kind in new[] { "forgeChain", "weapon", "weaponDiscard", "trade", "tradeAccept", "tradeReject", "tradeComplete", "tradeCancel" })
            Reject(0, Command(kind, from: 3, type: 2, slots: new[] { 3, 4 }), "unsupported_command");
        Apply(0, "forge", from: 3, to: 4);
        Assert.That(state.seats.All(seat => seat.virtues.Single().type == VirtueType.Art), Is.True);
        Assert.That(state.ruleset, Is.EqualTo(Definitions.LegacyRuleset));
    }

    [Test]
    public void LegacyJsonWithoutNewFieldsStillLoadsAndKeepsItsRuleset()
    {
        state.ruleset = Definitions.LegacyRuleset;
        Apply(0, "draw");
        var json = System.Text.Json.Nodes.JsonNode.Parse(Encode(state))!;
        json["pending"]!.AsObject().Remove("offerFrom");
        json["pending"]!.AsObject().Remove("offerResourceType");
        state = System.Text.Json.JsonSerializer.Deserialize<MatchState>(json.ToJsonString(), Json)!;
        DomainRules.ValidateState(state);
        Assert.That(state.pending.offerFrom, Is.EqualTo(-1));
        Apply(0, "place", to: 1);
        Assert.That(state.ruleset, Is.EqualTo(Definitions.LegacyRuleset));
        var command = System.Text.Json.JsonSerializer.Deserialize<OnlineCommand>(
            """{"kind":"move","from":0,"to":1}""", Json)!;
        Assert.That(command.slots, Is.Empty);
    }

    [Test]
    public void OrderedChainAccumulatesRecipientsEvenAfterLeavingTheirBoard()
    {
        Board((2, ResourceType.Fire), (3, ResourceType.Water), (4, ResourceType.Earth),
            (12, ResourceType.Air), (11, ResourceType.Fire), (10, ResourceType.Earth),
            (16, ResourceType.Water), (20, ResourceType.Water));
        Assert.That(Choices(0), Does.Contain("forgeChain"));
        Apply(0, "forgeChain", slots: new[] { 2, 3, 4, 12, 11, 10 });
        Assert.That(state.seats[0].virtues.Select(token => token.type),
            Is.EquivalentTo(new[] { VirtueType.Art, VirtueType.Nature, VirtueType.Energy, VirtueType.Wisdom, VirtueType.Security }));
        Assert.That(state.seats[1].virtues.Select(token => token.type),
            Is.EquivalentTo(new[] { VirtueType.Nature, VirtueType.Energy, VirtueType.Wisdom, VirtueType.Security }));
        Assert.That(state.seats.SelectMany(seat => seat.virtues).Select(token => token.id).Distinct().Count(), Is.EqualTo(9));
        Assert.That(state.board.Count(slot => slot.pieceId != null), Is.EqualTo(2));
        Assert.That(state.pending.kind, Is.EqualTo("Retraction"));
        Assert.That(Projection.ForSeat(state, 1).choices.Select(choice => choice.kind), Is.EqualTo(new[] { "pass" }));
    }

    [TestCase(new int[] { })]
    [TestCase(new[] { 0 })]
    [TestCase(new[] { 0, 1, 0 })]
    [TestCase(new[] { 0, 1, 2 })]
    [TestCase(new[] { 0, 1, 10 })]
    [TestCase(new[] { 0, 1, 32 })]
    [TestCase(new[] { -1, 0 })]
    [TestCase(new[] { 4, 3 })]
    [TestCase(new[] { 0, 8 })]
    [TestCase(new[] { 7, 8 })]
    public void InvalidChainNeverConsumesPrefixOrAwardsVirtues(int[] slots)
    {
        Board((0, ResourceType.Fire), (1, ResourceType.Water), (2, ResourceType.Water),
            (3, ResourceType.Air), (4, ResourceType.Fire), (7, ResourceType.Fire), (8, ResourceType.Fire));
        Assert.That(OrdinarySelection.Error(state.board, 0, slots, false), Is.Not.Null);
        Reject(0, Command("forgeChain", slots: slots), "invalid_forge");
    }

    [Test]
    public void NullAndOversizeSelectionsFailSafely()
    {
        var command = Command("forgeChain");
        command.slots = null!;
        Reject(0, command, "invalid_forge");
        Reject(0, Command("forgeChain", slots: Enumerable.Range(0, 33).ToArray()), "invalid_forge");
    }

    [Test]
    public void FullBoardSnakeChainIsBoundedAndConsumesEveryResourceOnce()
    {
        Board(Enumerable.Range(0, 32).Select(id => (id, (id / 8 + id % 8) % 2 == 0
            ? ResourceType.Fire : ResourceType.Water)).ToArray());
        var chain = Enumerable.Range(0, 4).SelectMany(row =>
            Enumerable.Range(0, 8).Select(column => row * 8 + (row % 2 == 0 ? column : 7 - column))).ToArray();
        Apply(0, "forgeChain", slots: chain);
        Assert.That(state.board.All(slot => slot.pieceId == null), Is.True);
        Assert.That(state.seats[0].virtues, Has.Count.EqualTo(31));
        Assert.That(state.seats[1].virtues, Has.Count.EqualTo(28));
    }

    [Test]
    public void MarkersAreAbsentWhenNoSelectionCanComplete()
    {
        Board((0, ResourceType.Water), (4, ResourceType.Fire));
        Assert.That(Choices(0), Does.Not.Contain("forgeChain").And.Not.Contain("weapon"));
        Board((0, ResourceType.Water), (1, ResourceType.Fire), (4, ResourceType.Earth));
        Assert.That(Choices(0), Does.Contain("forgeChain").And.Not.Contain("weapon"));
    }
}
