using System.Linq;
using NUnit.Framework;

public class BotPlannerTests
{
    [Test]
    public void WinterPrefersSurplusAndOnlyOffersOwnedVirtues()
    {
        var goals = new System.Collections.Generic.Dictionary<VirtueType, int> { [VirtueType.Nature] = 2 };
        var paths = BotPlanner.EvaluateVirtueDiscard(new[] { VirtueType.Nature, VirtueType.Art }, goals);
        Assert.That(paths[0].DiscardType, Is.EqualTo(VirtueType.Art));
        Assert.That(paths[0].Score, Is.EqualTo(-15));
        Assert.That(paths[1].Score, Is.EqualTo(-100));
        Assert.That(paths.All(path => path.Kind == BotActionKind.DiscardVirtue), Is.True);
        Assert.That(BotPlanner.EvaluateVirtueDiscard(System.Array.Empty<VirtueType>(), goals), Is.Empty);
        var duplicate = BotPlanner.EvaluateVirtueDiscard(
            new[] { VirtueType.Nature, VirtueType.Nature, VirtueType.Nature }, goals);
        Assert.That(duplicate.Count, Is.EqualTo(1));
        Assert.That(duplicate[0].Score, Is.EqualTo(-15));
        var requiredOnly = BotPlanner.EvaluateVirtueDiscard(new[] { VirtueType.Nature }, goals);
        Assert.That(requiredOnly[0].DiscardType, Is.EqualTo(VirtueType.Nature));
        Assert.That(requiredOnly[0].Score, Is.EqualTo(-100), "A mandatory discard cannot pass when only goal virtues remain.");
        var ties = BotPlanner.EvaluateVirtueDiscard(new[] { VirtueType.Energy, VirtueType.Art },
            new System.Collections.Generic.Dictionary<VirtueType, int>());
        Assert.That(ties[0].DiscardType, Is.EqualTo(VirtueType.Art));
    }

    private static BotCell[] Line(params ResourceType?[] values) => values.Select((value, index) =>
        new BotCell(index, value, Enumerable.Range(0, values.Length).Where(other => System.Math.Abs(index - other) == 1))).ToArray();

    private static BotRecipe Recipe(int needed = 2, int stock = 12) =>
        new BotRecipe(VirtueType.Nature, ResourceType.Earth, ResourceType.Water, needed, stock);

    [Test]
    public void MissingGoalForgeOutscoresUnknownDrawWithExplainableTotal()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Earth, ResourceType.Water, null), new[] { Recipe() }, true);
        Assert.That(paths[0].Kind, Is.EqualTo(BotActionKind.Forge));
        Assert.That(paths[0].Score, Is.EqualTo(72));
        Assert.That(paths[0].Terms.Sum(term => term.Value), Is.EqualTo(paths[0].Score));
        Assert.That(paths.Any(path => path.Kind == BotActionKind.Draw), Is.True);
    }

    [Test]
    public void LastRequiredVirtueReceivesVictoryBonus()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Earth, ResourceType.Water), new[] { Recipe(1) }, false);
        Assert.That(paths[0].Score, Is.EqualTo(10072));
    }

    [Test]
    public void UsefulAdjacentMoveBeatsSpeculativeDraw()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Earth, null, ResourceType.Water), new[] { Recipe() }, true);
        Assert.That(paths[0].Kind, Is.EqualTo(BotActionKind.Move));
        Assert.That(paths[0].From, Is.EqualTo(0));
        Assert.That(paths[0].To, Is.EqualTo(1));
        Assert.That(paths[0].Score, Is.EqualTo(10));
    }

    [Test]
    public void RevealedResourceOffersOnlyPlacementsAndChoosesUsefulPair()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Water, null, null), new[] { Recipe() }, true, ResourceType.Earth);
        Assert.That(paths.All(path => path.Kind == BotActionKind.Place), Is.True);
        Assert.That(paths[0].To, Is.EqualTo(1));
        Assert.That(paths[0].Score, Is.EqualTo(20));
    }

    [Test]
    public void DepletedVirtueStockExcludesForge()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Earth, ResourceType.Water), new[] { Recipe(stock: 0) }, false);
        Assert.That(paths.All(path => path.Kind != BotActionKind.Forge), Is.True);
    }

    [Test]
    public void OccupiedAndNonAdjacentSlotsAreNotMoveTargets()
    {
        var paths = BotPlanner.Evaluate(Line(ResourceType.Earth, ResourceType.Fire, null, null), new[] { Recipe() }, false);
        Assert.That(paths.Any(path => path.Kind == BotActionKind.Move && path.From == 0), Is.False);
        Assert.That(paths.Where(path => path.Kind == BotActionKind.Move).All(path => path.From == 1 && path.To == 2), Is.True);
    }

    [Test]
    public void FullBoardNeverDrawsAndPendingPlacementHasNoFakePass()
    {
        BotCell[] board = Line(ResourceType.Earth, ResourceType.Earth);
        Assert.That(BotPlanner.Evaluate(board, new[] { Recipe() }, true).Any(path => path.Kind == BotActionKind.Draw), Is.False);
        Assert.That(BotPlanner.Evaluate(board, new[] { Recipe() }, true, ResourceType.Water), Is.Empty);
    }

    [Test]
    public void DrawExplainsAllHypotheticalOutcomesWithoutReadingDeck()
    {
        var draw = BotPlanner.Evaluate(Line((ResourceType?)null), new[] { Recipe() }, true)
            .Single(path => path.Kind == BotActionKind.Draw);
        Assert.That(draw.Terms.Count, Is.EqualTo(5));
        Assert.That(draw.Score, Is.EqualTo(3).Within(.001f));
    }

    [Test]
    public void EvaluationIsDeterministicAndDoesNotChangeInput()
    {
        BotCell[] board = Line(ResourceType.Earth, null, ResourceType.Water);
        var before = board.Select(cell => cell.Resource).ToArray();
        var first = BotPlanner.Evaluate(board, new[] { Recipe() }, true);
        var second = BotPlanner.Evaluate(board, new[] { Recipe() }, true);
        CollectionAssert.AreEqual(first.Select(path => path.Key), second.Select(path => path.Key));
        CollectionAssert.AreEqual(before, board.Select(cell => cell.Resource));
    }
}
