using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class KingdomPowerTests
{
    private GameObject root;
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private PowerManager manager;
    private Player first;
    private Player second;
    private Virtue art;
    private Virtue nature;
    private int completed;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Kingdom power test");
        PlayerManager.Instance = Component<PlayerManager>("Players");
        PlayerManager.Instance.Players = new List<Player>();
        TurnManager.Instance = Component<TurnManager>("Turns");
        PowerManager.Instance = manager = Component<PowerManager>("Powers");
        ActionManager.Instance = Component<ActionManager>("Actions");
        SelectionManager.Instance = Component<SelectionManager>("Selection");
        BoardManager.Instance = Component<BoardManager>("Boards");
        BoardManager.Instance.gameBoards = new List<Board>();
        ForgeManager.Instance = Component<ForgeManager>("Forge");
        art = Asset<Virtue>();
        art.type = VirtueType.Art;
        nature = Asset<Virtue>();
        nature.type = VirtueType.Nature;
        ForgeManager.Instance.AllVirtues = new List<Virtue> { art, nature };
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            Resource prefab = Component<Resource>(type + " prefab");
            prefab.resourceType = type;
            SetField(BoardManager.Instance, type + "Prefab", prefab.gameObject);
        }
        first = Player("First", 8);
        second = Player("Second", 8);
        SetField(TurnManager.Instance, "activePlayer", first);
        completed = 0;
    }

    [TearDown]
    public void TearDown()
    {
        // Disable resources before managers so their static event subscriptions are removed.
        root.SetActive(false);
        Object.DestroyImmediate(root);
        foreach (ScriptableObject asset in assets)
            Object.DestroyImmediate(asset);
        assets.Clear();
        BoardManager.slots.Clear();
        PlayerManager.Instance = null;
        PowerManager.Instance = null;
        TurnManager.Instance = null;
        ActionManager.Instance = null;
        SelectionManager.Instance = null;
        BoardManager.Instance = null;
        ForgeManager.Instance = null;
    }

    [TestCase(0, 0, 0, true)]
    [TestCase(2, 2, 2, true)]
    [TestCase(1, 2, 2, false)]
    [TestCase(2, 1, 2, false)]
    [TestCase(3, 3, 2, false)]
    [TestCase(0, 0, -1, false)]
    public void PaymentRequiresExactOwnedMultiplicityWithoutMutation(
        int ownedCount, int selectedCount, int cost, bool expected)
    {
        var owned = Enumerable.Repeat(art, ownedCount).ToList();
        var selected = Enumerable.Repeat(art, selectedCount).ToList();
        var beforeOwned = owned.ToArray();
        var beforeSelected = selected.ToArray();
        Assert.That(PowerRules.CanPay(owned, selected, cost), Is.EqualTo(expected));
        CollectionAssert.AreEqual(beforeOwned, owned);
        CollectionAssert.AreEqual(beforeSelected, selected);
    }

    [Test]
    public void PaymentRejectsUnownedNullAndMissingLists()
    {
        var owned = new List<Virtue> { art };
        Assert.That(PowerRules.CanPay(owned, new List<Virtue> { nature }, 1), Is.False);
        Assert.That(PowerRules.CanPay(owned, new List<Virtue> { null }, 1), Is.False);
        Assert.That(PowerRules.CanPay(null, new List<Virtue>(), 0), Is.False);
        Assert.That(PowerRules.CanPay(owned, null, 0), Is.False);
        CollectionAssert.AreEqual(new[] { art }, owned);
    }

    [Test]
    public void PowerVerificationRejectsRepeatedSelectionNotOwnedTwice()
    {
        Equip<Magic>(first, 2);
        first.Virtues.AddRange(new[] { art, nature });
        first.selectedVirtue.AddRange(new[] { art, art });
        Assert.That(Power.PowerVerification(first), Is.False);
        first.selectedVirtue[1] = nature;
        Assert.That(Power.PowerVerification(first), Is.True);
    }

    [TestCase(typeof(IdentitySurfing))]
    [TestCase(typeof(InfiniteKnowledge))]
    [TestCase(typeof(Imagination))]
    [TestCase(typeof(Invisibility))]
    public void TargetedPowersRejectMissingSelfAndNonParticipantTargets(Type powerType)
    {
        PowerUse use = Use(first, Equip(first, powerType, 0));
        Assert.That(manager.Validate(use, out _), Is.False);
        use.Target = first;
        Assert.That(manager.Validate(use, out _), Is.False);
        Player outsider = Player("Outsider", 1);
        PlayerManager.Instance.Players.Remove(outsider);
        use.Target = outsider;
        Assert.That(manager.Validate(use, out _), Is.False);
        Assert.That(completed, Is.Zero);
    }

    [Test]
    public void ImaginationRejectsAnUnavailableVirtueWithoutSpending()
    {
        PowerUse use = Use(first, Equip<Imagination>(first, 1), second);
        first.Virtues.Add(art);
        use.Payment.Add(art);
        second.Virtues.Add(art);
        use.ChosenVirtue = nature;
        Assert.That(manager.Validate(use, out _), Is.False);
        CollectionAssert.AreEqual(new[] { art }, first.Virtues);
        use.ChosenVirtue = art;
        Assert.That(manager.Validate(use, out _), Is.True);
    }

    [Test]
    public void TransformRequiresExchangeInAdditionToActivationPayment()
    {
        PowerUse use = Use(first, Equip<TransformPower>(first, 1));
        first.Virtues.Add(art);
        use.Payment.Add(art);
        use.Exchange.Add(art);
        use.ChosenVirtue = nature;
        Assert.That(manager.Validate(use, out _), Is.False);
        first.Virtues.Add(art);
        Assert.That(manager.Validate(use, out _), Is.True);
        Assert.That(first.Virtues.Count, Is.EqualTo(2));
    }

    [Test]
    public void TransformStockIncludesReturnedPaymentButCannotOverdrawStockpile()
    {
        PowerUse use = Use(first, Equip<TransformPower>(first, 1));
        first.Virtues.AddRange(new[] { nature, art, art });
        second.Virtues.AddRange(Enumerable.Repeat(nature, 11));
        use.Payment.Add(nature);
        use.Exchange.AddRange(new[] { art, art });
        use.ChosenVirtue = nature;
        Assert.That(manager.Validate(use, out _), Is.False);
        use.Exchange.RemoveAt(1);
        Assert.That(manager.Validate(use, out _), Is.True);
        Assert.That(PowerRules.VirtueStock(nature), Is.Zero);
        Assert.That(first.Virtues.Count, Is.EqualTo(3));
    }

    [Test]
    public void InvisibilityRequiresResourceAndEmptyDestinationBeforeCharging()
    {
        PowerUse use = Use(first, Equip<Invisibility>(first, 0), second);
        Assert.That(manager.Validate(use, out _), Is.False);
        Piece(second, 0, ResourceType.Fire);
        Assert.That(manager.Validate(use, out _), Is.True);
        for (int i = 0; i < first.Board.Slots.Count; i++)
            Piece(first, i, ResourceType.Air);
        Assert.That(manager.Validate(use, out _), Is.False);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    public void RainValidatesItsOneToThreeResourceLimit(int count, bool valid)
    {
        PowerUse use = Use(first, Equip<Rain>(first, 0));
        use.ResourceCount = count;
        Assert.That(manager.Validate(use, out _), Is.EqualTo(valid));
    }

    [Test]
    public void HiddenOnlyPowerBecomesUnavailableAfterReveal()
    {
        Power power = Equip<Abundance>(first, 1);
        power.onlyWhileHidden = true;
        first.Virtues.Add(art);
        Assert.That(manager.CanUse(first), Is.True);
        first.kingdomRevealed = true;
        Assert.That(manager.CanUse(first), Is.False);
        power.onlyWhileHidden = false;
        Assert.That(manager.CanUse(first), Is.True);
    }

    [Test]
    public void IdentitySurfingSwapsKingdomAndRevealStateOnly()
    {
        Power power = Equip<IdentitySurfing>(first, 0);
        Equip<Magic>(second, 0);
        Kingdom originalFirst = first.Kingdom;
        Kingdom originalSecond = second.Kingdom;
        first.kingdomRevealed = true;
        second.kingdomRevealed = false;
        first.virtuesHidden = true;
        first.Virtues.Add(art);
        second.Virtues.Add(nature);
        Resource firstPiece = Piece(first, 0, ResourceType.Fire);
        Resource secondPiece = Piece(second, 1, ResourceType.Water);
        Board firstBoard = first.Board;
        Board secondBoard = second.Board;
        power.Execute(Use(first, power, second));
        Assert.That(first.Kingdom, Is.SameAs(originalSecond));
        Assert.That(second.Kingdom, Is.SameAs(originalFirst));
        Assert.That(first.kingdomRevealed, Is.False);
        Assert.That(second.kingdomRevealed, Is.True);
        Assert.That(first.virtuesHidden, Is.True);
        Assert.That(second.virtuesHidden, Is.False);
        Assert.That(first.Board, Is.SameAs(firstBoard));
        Assert.That(second.Board, Is.SameAs(secondBoard));
        Assert.That(first.Board.Slots[0].resource, Is.SameAs(firstPiece));
        Assert.That(second.Board.Slots[1].resource, Is.SameAs(secondPiece));
        CollectionAssert.AreEqual(new[] { art }, first.Virtues);
        CollectionAssert.AreEqual(new[] { nature }, second.Virtues);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void ImaginationTransfersExactlyOneOfDuplicateVirtues()
    {
        Power power = Equip<Imagination>(first, 0);
        second.Virtues.AddRange(new[] { art, art, nature });
        PowerUse use = Use(first, power, second);
        use.ChosenVirtue = art;
        power.Execute(use);
        CollectionAssert.AreEqual(new[] { art }, first.Virtues);
        CollectionAssert.AreEqual(new[] { art, nature }, second.Virtues);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void TransformProducesOnlyChosenTypeAndDoesNotChargeActivationTwice()
    {
        Power power = Equip<TransformPower>(first, 2);
        first.Virtues.AddRange(new[] { art, art, nature });
        PowerUse use = Use(first, power);
        use.Payment.AddRange(new[] { art, art });
        use.Exchange.AddRange(new[] { art, nature });
        use.ChosenVirtue = nature;
        power.Execute(use);
        CollectionAssert.AreEqual(new[] { art, nature, nature }, first.Virtues);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void InvisibilityHidesVirtuesOnlyAfterResourceAndDestinationChoices()
    {
        Power power = Equip<Invisibility>(first, 0);
        Resource stolen = Piece(second, 2, ResourceType.Earth);
        power.Execute(Use(first, power, second));
        Assert.That(first.virtuesHidden, Is.False);
        Choose("Earth at space 3");
        Assert.That(first.virtuesHidden, Is.False);
        Assert.That(completed, Is.Zero);
        Choose("Space 5");
        Assert.That(first.virtuesHidden, Is.True);
        Assert.That(first.CanSeeVirtues(first), Is.True);
        Assert.That(first.CanSeeVirtues(second), Is.False);
        Assert.That(first.Board.Slots[4].resource, Is.SameAs(stolen));
        Assert.That(stolen.slot, Is.SameAs(first.Board.Slots[4]));
        Assert.That(second.Board.Slots[2].resource, Is.Null);
        Assert.That(second.Board.Slots[2].isOccupied, Is.False);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void WitchcraftAllowsRepeatedMovesAndSwapsUntilExplicitFinish()
    {
        Power power = Equip<Witchcraft>(first, 0);
        Resource fire = Piece(first, 0, ResourceType.Fire);
        Resource air = Piece(first, 1, ResourceType.Air);
        power.Execute(Use(first, power));
        Choose("Fire at space 1");
        Choose("Space 3");
        Choose("Fire at space 3");
        Choose("Space 2 (swap with Air)");
        Assert.That(first.Board.Slots[1].resource, Is.SameAs(fire));
        Assert.That(first.Board.Slots[2].resource, Is.SameAs(air));
        Assert.That(first.Board.Slots[0].isOccupied, Is.False);
        Assert.That(completed, Is.Zero);
        Choose("Finish rearranging");
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(PowerRules.Resources(first).Count, Is.EqualTo(2));
    }

    [Test]
    public void WitchcraftCanSwapAndFinishOnAFullBoard()
    {
        Power power = Equip<Witchcraft>(first, 0);
        for (int i = 0; i < first.Board.Slots.Count; i++)
            Piece(first, i, ResourceType.Fire);
        Resource original = first.Board.Slots[0].resource;
        Resource swapped = first.Board.Slots[7].resource;
        power.Execute(Use(first, power));
        Choose("Fire at space 1");
        Choose("Space 8 (swap with Fire)");
        Choose("Finish rearranging");
        Assert.That(first.Board.Slots[7].resource, Is.SameAs(original));
        Assert.That(first.Board.Slots[0].resource, Is.SameAs(swapped));
        Assert.That(PowerRules.EmptySlots(first), Is.Empty);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void BlessingDuplicatesTheInitialMultisetRatherThanNewlyAddedPieces()
    {
        Power power = Equip<BlessingOfPlenty>(first, 0);
        Piece(first, 0, ResourceType.Fire);
        Piece(first, 1, ResourceType.Fire);
        Piece(first, 2, ResourceType.Water);
        power.Execute(Use(first, power));
        Add(ResourceType.Fire, 4);
        Add(ResourceType.Fire, 5);
        CollectionAssert.DoesNotContain(manager.Choices.Select(choice => choice.Label).ToArray(), "Fire");
        Add(ResourceType.Water, 6);
        Assert.That(PowerRules.Resources(first).Count(resource => resource.resourceType == ResourceType.Fire), Is.EqualTo(4));
        Assert.That(PowerRules.Resources(first).Count(resource => resource.resourceType == ResourceType.Water), Is.EqualTo(2));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void BlessingLetsPlayerChooseWhichCopyFitsWhenSpaceIsLimited()
    {
        Player small = Player("Small", 4);
        Power power = Equip<BlessingOfPlenty>(small, 0);
        Piece(small, 0, ResourceType.Fire);
        Piece(small, 1, ResourceType.Air);
        Piece(small, 2, ResourceType.Water);
        power.Execute(Use(small, power));
        CollectionAssert.AreEquivalent(new[] { "Fire", "Air", "Water" }, manager.Choices.Select(choice => choice.Label));
        Add(ResourceType.Water, 4);
        Choose("Continue");
        Assert.That(PowerRules.Resources(small).Count, Is.EqualTo(4));
        Assert.That(small.Board.Slots[3].resource.resourceType, Is.EqualTo(ResourceType.Water));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void BlessingSkipsExhaustedStockAndStillAddsOtherCopies()
    {
        Power power = Equip<BlessingOfPlenty>(first, 0);
        Piece(first, 0, ResourceType.Fire);
        Piece(first, 1, ResourceType.Water);
        Player stockHolder = Player("Stock holder", 19);
        for (int i = 0; i < 19; i++)
            Piece(stockHolder, i, ResourceType.Fire);
        power.Execute(Use(first, power));
        CollectionAssert.AreEqual(new[] { "Water" }, manager.Choices.Select(choice => choice.Label));
        Add(ResourceType.Water, 3);
        Choose("Continue");
        Assert.That(PowerRules.Resources(first).Count, Is.EqualTo(3));
        Assert.That(PowerRules.ResourceStock(ResourceType.Fire), Is.Zero);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void AbundanceAddsOneResourcePerParticipant()
    {
        Power power = Equip<Abundance>(first, 0);
        Player("Third", 1);
        power.Execute(Use(first, power));
        Add(ResourceType.Fire, 1);
        Add(ResourceType.Air, 2);
        Add(ResourceType.Earth, 3);
        Assert.That(PowerRules.Resources(first).Count, Is.EqualTo(3));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void RainAddsForEveryPlayerWithIndependentChoicesAndBestEffortCapacity()
    {
        PlayerManager.Instance.Players.Clear();
        Player small = Player("Small", 2);
        Player full = Player("Full", 1);
        Player empty = Player("Empty", 2);
        Piece(small, 0, ResourceType.Fire);
        Piece(full, 0, ResourceType.Earth);
        Power power = Equip<Rain>(small, 0);
        PowerUse use = Use(small, power);
        use.ResourceCount = 3;
        power.Execute(use);
        Assert.That(manager.ChoiceTitle, Does.StartWith("Small:"));
        Add(ResourceType.Air, 2);
        Choose("Continue");
        Assert.That(manager.ChoiceTitle, Does.StartWith("Full:"));
        Choose("Continue");
        Assert.That(manager.ChoiceTitle, Does.StartWith("Empty:"));
        Add(ResourceType.Water, 2);
        Add(ResourceType.Earth, 1);
        Choose("Continue");
        Assert.That(small.Board.Slots[1].resource.resourceType, Is.EqualTo(ResourceType.Air));
        Assert.That(PowerRules.Resources(full).Count, Is.EqualTo(1));
        Assert.That(empty.Board.Slots[1].resource.resourceType, Is.EqualTo(ResourceType.Water));
        Assert.That(empty.Board.Slots[0].resource.resourceType, Is.EqualTo(ResourceType.Earth));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void RainRemovesChosenResourcesForEveryPlayerAndSkipsEmptyBoards()
    {
        Power power = Equip<Rain>(first, 0);
        Piece(first, 0, ResourceType.Fire);
        Piece(first, 1, ResourceType.Air);
        Resource retained = Piece(first, 2, ResourceType.Water);
        Player third = Player("Third", 2);
        Piece(third, 1, ResourceType.Earth);
        PowerUse use = Use(first, power);
        use.ResourceCount = 2;
        use.RemoveResources = true;
        power.Execute(use);
        Remove("Air at space 2");
        Remove("Fire at space 1");
        Assert.That(manager.ChoiceTitle, Does.StartWith("Second:"));
        Choose("Continue");
        Assert.That(manager.ChoiceTitle, Does.StartWith("Third:"));
        Remove("Earth at space 2");
        Choose("Continue");
        CollectionAssert.AreEqual(new[] { retained }, PowerRules.Resources(first));
        Assert.That(PowerRules.Resources(second), Is.Empty);
        Assert.That(PowerRules.Resources(third), Is.Empty);
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void SnapshotPaymentUsesOriginalMultiplicityNotLaterGains()
    {
        first.Virtues.AddRange(new[] { art, art });
        TurnSnapshot snapshot = TurnSnapshot.Capture();
        first.Virtues.Clear();
        first.Virtues.Add(nature);
        Assert.That(snapshot.CanPay(first, new List<Virtue> { art, art }), Is.True);
        Assert.That(snapshot.CanPay(first, new List<Virtue> { art, art, art }), Is.False);
        Assert.That(snapshot.CanPay(first, new List<Virtue> { nature }), Is.False);
    }

    [Test]
    public void InfiniteKnowledgeRevealsOnlyToItsCasterUntilAcknowledged()
    {
        Power power = Equip<InfiniteKnowledge>(first, 0);
        Equip<Magic>(second, 0);
        Player observer = Player("Observer", 1);
        power.Execute(Use(first, power, second));
        Assert.That(second.CanSeeKingdom(first), Is.True);
        Assert.That(second.CanSeeKingdom(observer), Is.False);
        Assert.That(second.kingdomRevealed, Is.False);
        Assert.That(completed, Is.Zero);
        Choose("Continue");
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(first.knownKingdoms, Does.Contain(second.Kingdom));
    }

    [Test]
    public void RetractionRestoresActionSnapshotAndKeepsItsOwnPaymentSpent()
    {
        Equip<Retraction>(second, 1).timing = PowerTiming.OtherTurn;
        first.Virtues.Add(art);
        second.Virtues.Add(nature);
        manager.BeginNormalTurn(first);
        first.Virtues.Add(nature);
        first.hasDrawnResource = true;
        manager.CompleteAction(first, () => completed++);
        Choose("Use power");
        Choose("+ Nature (0/1)");
        Choose("Continue");
        Choose("Use power");
        CollectionAssert.AreEqual(new[] { art }, first.Virtues);
        Assert.That(second.Virtues, Is.Empty);
        Assert.That(second.kingdomRevealed, Is.True);
        Assert.That(first.hasDrawnResource, Is.False);
        Assert.That(completed, Is.Zero);
        Choose("Continue");
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(manager.IsBusy, Is.False);
    }

    [Test]
    public void RetractionRejectsPaymentAcquiredOnlyDuringTheUndoneAction()
    {
        Power power = Equip<Retraction>(second, 1);
        manager.BeginNormalTurn(first);
        second.Virtues.Add(art);
        PowerUse use = Use(second, power, first);
        use.Payment.Add(art);
        Assert.That(manager.Validate(use, out _), Is.False);
        CollectionAssert.AreEqual(new[] { art }, second.Virtues);
    }

    [Test]
    public void RetractionPreservesEarlierTimeReactionPaymentAndQueuedTurn()
    {
        Equip<TimePower>(second, 2).timing = PowerTiming.OtherTurn;
        Player retractor = Player("Retractor", 1);
        Equip<Retraction>(retractor, 2).timing = PowerTiming.OtherTurn;
        first.Virtues.Add(nature);
        second.Virtues.AddRange(new[] { art, art, nature });
        retractor.Virtues.AddRange(new[] { nature, nature, art });
        manager.BeginNormalTurn(first);
        Choose("Pass");
        first.Virtues.Add(art);

        manager.CompleteAction(first, () => completed++);
        Assert.That(manager.ChoiceTitle, Does.Contain("Second: TimePower"));
        Choose("Use power");
        Choose("+ Art (0/2)");
        Choose("+ Art (1/2)");
        Choose("Continue");
        Choose("Use power");
        CollectionAssert.AreEqual(new[] { nature }, second.Virtues);

        Assert.That(manager.ChoiceTitle, Does.Contain("Retractor: Retraction"));
        Choose("Use power");
        Choose("+ Nature (0/2)");
        Choose("+ Nature (1/2)");
        Choose("Continue");
        Choose("Use power");
        CollectionAssert.AreEqual(new[] { nature }, first.Virtues);
        CollectionAssert.AreEqual(new[] { nature }, second.Virtues);
        CollectionAssert.AreEqual(new[] { art }, retractor.Virtues);
        Assert.That(second.kingdomRevealed, Is.True);
        Assert.That(retractor.kingdomRevealed, Is.True);
        Assert.That(completed, Is.Zero);
        Choose("Continue");
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(manager.IsBusy, Is.False);
        Assert.That(manager.NextPlayer(retractor), Is.SameAs(second));
        Assert.That(manager.NextPlayer(first), Is.SameAs(retractor));
    }

    [Test]
    public void PendingPowerChoiceBlocksResourceDiscardAndSpecialPhaseCompletion()
    {
        Resource resource = Piece(first, 0, ResourceType.Fire);
        FieldInfo resourceCounts = typeof(Resource).GetField("ResourceCount",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(resourceCounts, Is.Not.Null);
        object previousCounts = resourceCounts.GetValue(null);
        var counts = new Dictionary<Player, int> { { first, 1 } };
        resourceCounts.SetValue(null, counts);
        try
        {
            int specialCompleted = 0;
            TurnManager.Instance.OnSpecialTurn(true, new List<Player> { first }, () => specialCompleted++);
            manager.Choose("Pending choice",
                new List<PowerChoice> { new PowerChoice("Finish", () => completed++) });
            LogAssert.Expect(LogType.Warning, "Finish the pending choice before discarding a resource.");
            resource.OnclickDestroy();
            Assert.That(first.Board.Slots[0].resource, Is.SameAs(resource));
            Assert.That(first.Board.Slots[0].isOccupied, Is.True);
            Assert.That(resource.slot, Is.SameAs(first.Board.Slots[0]));
            Assert.That(resource.gameObject.activeSelf, Is.True);
            Assert.That(counts[first], Is.EqualTo(1));
            Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.True);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
            Assert.That(specialCompleted, Is.Zero);
            Assert.That(manager.IsBusy, Is.True);
            Assert.That(manager.ChoiceTitle, Is.EqualTo("Pending choice"));
            Assert.That(completed, Is.Zero);
            Choose("Finish");
            Assert.That(completed, Is.EqualTo(1));
        }
        finally
        {
            resourceCounts.SetValue(null, previousCounts);
        }
    }

    [Test]
    public void SnapshotRestoresVirtuesKingdomsAndSelectionsWithoutHidingRevealedKnowledge()
    {
        Equip<Magic>(first, 0);
        Equip<Abundance>(second, 0);
        Kingdom original = first.Kingdom;
        Kingdom other = second.Kingdom;
        first.Virtues.AddRange(new[] { art, art });
        first.virtuesHidden = true;
        TurnSnapshot snapshot = TurnSnapshot.Capture();
        first.Kingdom = other;
        second.Kingdom = original;
        second.kingdomRevealed = true;
        first.Virtues.Clear();
        first.Virtues.Add(nature);
        first.virtuesHidden = false;
        first.hasDrawnResource = true;
        first.selectedVirtue.Add(nature);
        first.selectedSlots.Add(first.Board.Slots[0]);
        first.selectedPlayer = second;
        snapshot.Restore();
        Assert.That(first.Kingdom, Is.SameAs(original));
        Assert.That(second.Kingdom, Is.SameAs(other));
        Assert.That(first.kingdomRevealed, Is.True);
        Assert.That(second.kingdomRevealed, Is.False);
        Assert.That(first.virtuesHidden, Is.True);
        Assert.That(first.hasDrawnResource, Is.False);
        Assert.That(first.selectedVirtue, Is.Empty);
        Assert.That(first.selectedSlots, Is.Empty);
        Assert.That(first.selectedPlayer, Is.Null);
        CollectionAssert.AreEqual(new[] { art, art }, first.Virtues);
    }

    [Test]
    public void ChoiceCallbacksRejectDisabledAndStaleSelections()
    {
        int selected = 0;
        manager.Choose("Old", new List<PowerChoice> { new PowerChoice("Old", () => selected++) });
        PowerChoice old = manager.Choices[0];
        manager.Choose("New", new List<PowerChoice> { new PowerChoice("Disabled", () => selected++, false) });
        LogAssert.Expect(LogType.Warning, "That power choice is no longer available.");
        old.OnSelected();
        LogAssert.Expect(LogType.Warning, "That power choice is no longer available.");
        manager.Choices[0].OnSelected();
        Assert.That(selected, Is.Zero);
    }

    [Test]
    public void CancellingPreparationDoesNotSpendOrReveal()
    {
        Power power = Equip<Magic>(first, 1);
        first.Virtues.Add(art);
        manager.ActivatePower(power);
        Choose("+ Art (0/1)");
        Choose("Cancel / pass");
        CollectionAssert.AreEqual(new[] { art }, first.Virtues);
        Assert.That(first.kingdomRevealed, Is.False);
        Assert.That(manager.IsBusy, Is.False);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
    }

    [Test]
    public void CommittingChargesExactDuplicatePaymentAndRevealsOnlyOnce()
    {
        Power power = Equip<IdentitySurfing>(first, 2);
        Equip<Magic>(second, 0);
        Kingdom original = first.Kingdom;
        first.Virtues.AddRange(new[] { art, art, nature });
        manager.ActivatePower(power);
        Choose("+ Art (0/2)");
        Choose("+ Art (1/2)");
        Choose("Continue");
        Choose("Second");
        PowerChoice commit = manager.Choices.Single(choice => choice.Label == "Use power");
        commit.OnSelected();
        CollectionAssert.AreEqual(new[] { nature }, first.Virtues);
        Assert.That(second.Kingdom, Is.SameAs(original));
        Assert.That(second.kingdomRevealed, Is.True);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second));
        LogAssert.Expect(LogType.Warning, "That power choice is no longer available.");
        commit.OnSelected();
        CollectionAssert.AreEqual(new[] { nature }, first.Virtues);
    }

    [Test]
    public void KingsNecklaceCancelsEffectButBothActivationCostsRemainPaid()
    {
        Power power = Equip<IdentitySurfing>(first, 1);
        Equip<KingsNecklace>(second, 1).timing = PowerTiming.PowerReaction;
        Kingdom firstKingdom = first.Kingdom;
        Kingdom secondKingdom = second.Kingdom;
        first.Virtues.Add(art);
        second.Virtues.Add(nature);
        manager.ActivatePower(power);
        Choose("+ Art (0/1)");
        Choose("Continue");
        Choose("Second");
        Choose("Use power");
        Choose("Use power");
        Choose("+ Nature (0/1)");
        Choose("Continue");
        Choose("Use power");
        Choose("Continue");
        Assert.That(first.Kingdom, Is.SameAs(firstKingdom));
        Assert.That(second.Kingdom, Is.SameAs(secondKingdom));
        Assert.That(first.Virtues, Is.Empty);
        Assert.That(second.Virtues, Is.Empty);
        Assert.That(first.kingdomRevealed, Is.True);
        Assert.That(second.kingdomRevealed, Is.True);
        Assert.That(manager.IsBusy, Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CelestialDomeProtectionCanBePaidOrPassed(bool protect)
    {
        Equip<CelestialDome>(second, 1).timing = PowerTiming.ThreatReaction;
        second.Virtues.Add(nature);
        int prevented = 0;
        int proceeded = 0;
        manager.OfferProtection(second, "Threat", () => prevented++, () => proceeded++);
        if (protect)
        {
            Choose("Use power");
            Choose("+ Nature (0/1)");
            Choose("Continue");
            Choose("Use power");
        }
        else
            Choose("Pass");
        Assert.That(prevented, Is.EqualTo(protect ? 1 : 0));
        Assert.That(proceeded, Is.EqualTo(protect ? 0 : 1));
        Assert.That(second.Virtues.Count, Is.EqualTo(protect ? 0 : 1));
        Assert.That(manager.IsBusy, Is.False);
    }

    [Test]
    public void BeginNormalTurnOffersManipulationAndClearsControlAfterAction()
    {
        Equip<Manipulation>(second, 1).timing = PowerTiming.TurnStart;
        second.Virtues.Add(art);
        manager.BeginNormalTurn(first);
        Choose("Use power");
        Choose("+ Art (0/1)");
        Choose("Continue");
        Choose("Use power");
        Assert.That(manager.Controller, Is.SameAs(second));
        Assert.That(manager.Viewer, Is.SameAs(second));
        Choose("Continue");
        manager.CompleteAction(first, () => completed++);
        Assert.That(manager.Controller, Is.Null);
        Assert.That(manager.Viewer, Is.SameAs(first));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void MagicGrantsExactlyTwoAdditionalActionsAndResetsTheirSelections()
    {
        Power power = Equip<Magic>(first, 0);
        manager.BeginNormalTurn(first);
        power.Execute(Use(first, power));
        completed = 0;
        for (int i = 0; i < 2; i++)
        {
            first.hasDrawnResource = true;
            first.selectedSlots.Add(first.Board.Slots[0]);
            ActionManager.Instance.SetAction(ActionManager.ActionState.UsedPower);
            manager.CompleteAction(first, () => completed++);
            Assert.That(completed, Is.Zero);
            Assert.That(first.hasDrawnResource, Is.False);
            Assert.That(first.selectedSlots, Is.Empty);
            Choose("Continue");
            Assert.That(ActionManager.Instance.CanPerformAction(), Is.True);
        }
        manager.CompleteAction(first, () => completed++);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(manager.IsBusy, Is.False);
    }

    [Test]
    public void TimeQueuesInOrderAndResumesTheOriginalSequentialPlayer()
    {
        Power power = Equip<TimePower>(first, 0);
        Player third = Player("Third", 1);
        power.Execute(Use(first, power));
        manager.ScheduleTurn(third);
        Assert.That(manager.NextPlayer(second), Is.SameAs(first));
        Assert.That(manager.NextPlayer(first), Is.SameAs(third));
        Assert.That(manager.NextPlayer(first), Is.SameAs(second));
        Assert.That(manager.NextPlayer(third), Is.SameAs(third));
        Assert.That(completed, Is.EqualTo(1));
    }

    [Test]
    public void CatalogContainsFifteenDistinctKingdomsAndPowerAssets()
    {
        KingdomCatalog catalog = Catalog();
        Assert.That(catalog.kingdoms.Count, Is.EqualTo(15));
        Assert.That(catalog.kingdoms, Has.None.Null);
        Assert.That(catalog.kingdoms.Distinct().Count(), Is.EqualTo(15));
        Assert.That(catalog.kingdoms.Select(kingdom => kingdom.kingdomName).Distinct().Count(), Is.EqualTo(15));
        Assert.That(catalog.kingdoms.Select(kingdom => kingdom.power), Has.None.Null);
        Assert.That(catalog.kingdoms.Select(kingdom => kingdom.power).Distinct().Count(), Is.EqualTo(15));
    }

    [Test]
    public void PendingChoicesBlockSpecialTurnCompletion()
    {
        TurnManager.Instance.isSpecialCardDrawn = true;
        manager.Notice("Inspecting a player", () => completed++);
        LogAssert.Expect(LogType.Warning, "Finish the pending choice before completing a special turn.");

        TurnManager.Instance.CompleteSpecialTurn(first, () => completed++);

        Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.True);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
        Assert.That(completed, Is.Zero);
    }

    [Test]
    public void KingdomExchangeCanWinForTheOtherPlayer()
    {
        Power swap = Equip<IdentitySurfing>(first, 0);
        Equip<Magic>(second, 0);
        first.Kingdom.virtuesForWin = new[]
        {
            new Kingdom.VirtuesForCost { NumberofVirtues = 2, virtues = art }
        };
        second.Kingdom.virtuesForWin = new[]
        {
            new Kingdom.VirtuesForCost { NumberofVirtues = 3, virtues = nature }
        };
        second.Virtues.AddRange(new[] { art, art });
        GameManager game = Component<GameManager>("Game");
        game.WinScreen = new GameObject("Win screen");
        game.WinScreen.transform.SetParent(root.transform);
        game.WinScreen.SetActive(false);

        swap.Execute(Use(first, swap, second));
        game.CheckWinConditions();

        Assert.That(game.ActivePlayer, Is.SameAs(second));
        Assert.That(game.WinScreen.activeSelf, Is.True);
    }

    [TestCase("Egolica", typeof(Abundance), 0, PowerTiming.OwnTurn, "Security:2,Nature:3,Economy:4")]
    [TestCase("Konga", typeof(Magic), 2, PowerTiming.OwnTurn, "Art:3,Security:4,Nature:2")]
    [TestCase("The Aradas", typeof(IdentitySurfing), 3, PowerTiming.OwnTurn, "Art:2,Wisdom:3,Nature:4")]
    [TestCase("Eko Akete", typeof(TransformPower), 4, PowerTiming.OwnTurn, "Art:4,Wisdom:3,Economy:2")]
    [TestCase("ILAGIK", typeof(Witchcraft), 3, PowerTiming.OwnTurn, "Energy:4,Security:3,Nature:2")]
    [TestCase("Bis-Bese Avouman", typeof(KingsNecklace), 3, PowerTiming.PowerReaction, "Art:2,Nature:4,Economy:3")]
    [TestCase("Milu", typeof(Invisibility), 2, PowerTiming.OwnTurn, "Energy:2,Nature:3,Economy:4")]
    [TestCase("Empire Volta", typeof(CelestialDome), 1, PowerTiming.ThreatReaction, "Energy:3,Security:4,Wisdom:2")]
    [TestCase("N'evulandis", typeof(InfiniteKnowledge), 1, PowerTiming.OwnTurn, "Energy:4,Wisdom:2,Economy:3")]
    [TestCase("Royaume Kongo", typeof(Manipulation), 3, PowerTiming.TurnStart, "Art:4,Energy:2,Security:3")]
    [TestCase("Kavango Keendobe", typeof(Rain), 3, PowerTiming.OwnTurn, "Security:2,Wisdom:3,Economy:4")]
    [TestCase("Ubunifu", typeof(Imagination), 3, PowerTiming.OwnTurn, "Art:2,Energy:3,Wisdom:4")]
    [TestCase("Logone", typeof(BlessingOfPlenty), 4, PowerTiming.OwnTurn, "Wisdom:3,Nature:4,Economy:2")]
    [TestCase("Telalila", typeof(TimePower), 2, PowerTiming.OtherTurn, "Art:4,Energy:3,Nature:2")]
    [TestCase("Mask of Light", typeof(Retraction), 2, PowerTiming.OtherTurn, "Art:3,Security:4,Economy:2")]
    public void CatalogKingdomHasExpectedPowerCostTimingAndVictoryGoals(
        string name, Type powerType, int cost, PowerTiming timing, string goals)
    {
        Kingdom kingdom = Catalog().kingdoms.Single(item => item != null && item.kingdomName == name);
        Assert.That(kingdom.power, Is.Not.Null);
        Assert.That(kingdom.power.GetType(), Is.EqualTo(powerType));
        Assert.That(kingdom.power.virtueCost, Is.EqualTo(cost));
        Assert.That(kingdom.power.timing, Is.EqualTo(timing));
        Assert.That(kingdom.power.onlyWhileHidden, Is.EqualTo(powerType == typeof(Abundance)));
        Assert.That(kingdom.kingdomStory, Is.Not.Null.And.Not.Empty);
        Assert.That(kingdom.power.powerName, Is.Not.Null.And.Not.Empty);
        Assert.That(kingdom.power.powerDescription, Is.Not.Null.And.Not.Empty);
        Assert.That(kingdom.virtuesForWin, Has.Length.EqualTo(3));
        Assert.That(kingdom.virtuesForWin.Select(goal => goal.virtues), Has.None.Null);
        CollectionAssert.AreEquivalent(goals.Split(','),
            kingdom.virtuesForWin.Select(goal => goal.virtues.type + ":" + goal.NumberofVirtues));
        foreach (Kingdom.VirtuesForCost goal in kingdom.virtuesForWin)
            Assert.That(AssetDatabase.Contains(goal.virtues), Is.True);
    }

    private static KingdomCatalog Catalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<KingdomCatalog>(
            "Assets/Busara/ScriptableObjects/Kingdoms/KingdomCatalog.asset");
        Assert.That(catalog, Is.Not.Null, "The serialized kingdom catalog must import successfully.");
        return catalog;
    }

    private T Component<T>(string name) where T : Component
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(root.transform);
        return gameObject.AddComponent<T>();
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        assets.Add(asset);
        return asset;
    }

    private Player Player(string name, int spaces)
    {
        Player player = Component<Player>(name);
        player.Name = name;
        player.Board = Component<Board>(name + " board");
        player.Board.player = player;
        player.Board.Slots = new List<Slot>();
        for (int i = 0; i < spaces; i++)
        {
            Slot slot = Component<Slot>(name + " space " + i);
            slot.transform.SetParent(player.Board.transform);
            slot.Index = i;
            slot.board = player.Board;
            player.Board.Slots.Add(slot);
        }
        PlayerManager.Instance.Players.Add(player);
        BoardManager.Instance.gameBoards.Add(player.Board);
        return player;
    }

    private T Equip<T>(Player player, int cost) where T : Power
    {
        return (T)Equip(player, typeof(T), cost);
    }

    private Power Equip(Player player, Type type, int cost)
    {
        var power = (Power)ScriptableObject.CreateInstance(type);
        assets.Add(power);
        power.powerName = type.Name;
        power.virtueCost = cost;
        power.timing = PowerTiming.OwnTurn;
        player.Kingdom = Asset<Kingdom>();
        player.Kingdom.kingdomName = player.Name + " kingdom";
        player.Kingdom.power = power;
        player.Kingdom.virtuesForWin = new Kingdom.VirtuesForCost[0];
        return power;
    }

    private PowerUse Use(Player caster, Power power, Player target = null)
    {
        return new PowerUse { Caster = caster, Power = power, Target = target, Complete = () => completed++ };
    }

    private Resource Piece(Player player, int index, ResourceType type)
    {
        Resource resource = Component<Resource>(type + " piece");
        resource.resourceType = type;
        Board.PlaceResource(resource, player.Board.Slots[index]);
        return resource;
    }

    private void Choose(string label)
    {
        PowerChoice choice = manager.Choices.SingleOrDefault(item => item.Label == label);
        Assert.That(choice, Is.Not.Null, "Missing choice '" + label + "' in " + manager.ChoiceTitle);
        Assert.That(choice.Enabled, Is.True, "Disabled choice: " + label);
        choice.OnSelected();
    }

    private void Add(ResourceType type, int space)
    {
        Choose(type.ToString());
        Choose("Space " + space);
    }

    private void Remove(string label)
    {
        // Runtime removal uses deferred Destroy; the fixture owns immediate EditMode cleanup.
        LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
        Choose(label);
    }

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + name);
        field.SetValue(instance, value);
    }
}
