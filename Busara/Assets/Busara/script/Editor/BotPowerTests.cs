using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class BotPowerTests
{
    private GameObject root;
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Player owner;
    private Player other;
    private Virtue art;
    private Virtue nature;
    private PowerManager manager;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Bot power fixture");
        PlayerManager.Instance = Component<PlayerManager>("Players");
        PlayerManager.Instance.Players = new List<Player>();
        TurnManager.Instance = Component<TurnManager>("Turns");
        PowerManager.Instance = manager = Component<PowerManager>("Powers");
        ActionManager.Instance = Component<ActionManager>("Actions");
        SelectionManager.Instance = Component<SelectionManager>("Selections");
        BoardManager.Instance = Component<BoardManager>("Boards");
        BoardManager.Instance.gameBoards = new List<Board>();
        ForgeManager.Instance = Component<ForgeManager>("Forge");
        art = Asset<Virtue>();
        art.type = VirtueType.Art;
        art.componentOne = ResourceType.Fire;
        art.componentTwo = ResourceType.Water;
        nature = Asset<Virtue>();
        nature.type = VirtueType.Nature;
        nature.componentOne = ResourceType.Earth;
        nature.componentTwo = ResourceType.Air;
        ForgeManager.Instance.AllVirtues = new List<Virtue> { art, nature };
        owner = MakePlayer("Decision owner");
        other = MakePlayer("Active other");
        Equip(owner, typeof(Magic), 0);
        Equip(other, typeof(Magic), 0);
        SetField(TurnManager.Instance, "activePlayer", other);
        BoardManager.slots.Clear();
        BoardManager.slots.AddRange(owner.Board.Slots);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (root != null)
            {
                root.SetActive(false);
                Object.DestroyImmediate(root);
            }
        }
        finally
        {
            foreach (ScriptableObject asset in assets)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            assets.Clear();
            BoardManager.slots.Clear();
            PlayerManager.Instance = null;
            TurnManager.Instance = null;
            PowerManager.Instance = null;
            ActionManager.Instance = null;
            SelectionManager.Instance = null;
            BoardManager.Instance = null;
            ForgeManager.Instance = null;
        }
    }

    [TestCase(typeof(Abundance))]
    [TestCase(typeof(BlessingOfPlenty))]
    [TestCase(typeof(CelestialDome))]
    [TestCase(typeof(IdentitySurfing))]
    [TestCase(typeof(Imagination))]
    [TestCase(typeof(InfiniteKnowledge))]
    [TestCase(typeof(Invisibility))]
    [TestCase(typeof(KingsNecklace))]
    [TestCase(typeof(Magic))]
    [TestCase(typeof(Manipulation))]
    [TestCase(typeof(Rain))]
    [TestCase(typeof(Retraction))]
    [TestCase(typeof(TimePower))]
    [TestCase(typeof(TransformPower))]
    [TestCase(typeof(Witchcraft))]
    public void EveryCatalogPowerHasAnExplicitPolicy(Type type)
    {
        Kingdom kingdom = Catalog().kingdoms.Single(item => item.power.GetType() == type);
        Assert.That(BotPowerPlanner.Supports(kingdom.power), Is.True);
        owner.Kingdom = kingdom;
        owner.Virtues.AddRange(Enumerable.Repeat(nature, kingdom.power.virtueCost));
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        Assert.That(plan.Use.Power, Is.SameAs(kingdom.power));
        Assert.That(plan.Use.Payment.Count, Is.EqualTo(kingdom.power.virtueCost));
        Assert.That(float.IsNaN(plan.Score), Is.False);
        Assert.That(BotPowerPlanner.Supports(null), Is.False);
    }

    [Test]
    public void PurePaymentLossCountsDuplicatesGoalsSurplusAndVictoryWithoutMutation()
    {
        Goals(owner, art, 2);
        var owned = new List<Virtue> { art, art, art, nature };
        var payment = new List<Virtue> { art, art };
        var before = owned.ToArray();
        Assert.That(BotPowerPlanner.InventoryValue(owner.Kingdom, new[] { VirtueType.Nature }), Is.EqualTo(15));
        Assert.That(BotPowerPlanner.PaymentLoss(owner.Kingdom, owned, new[] { nature }), Is.EqualTo(15));
        Assert.That(BotPowerPlanner.PaymentLoss(owner.Kingdom, owned, new[] { art }), Is.EqualTo(15));
        Assert.That(BotPowerPlanner.PaymentLoss(owner.Kingdom, owned, payment), Is.EqualTo(10115));
        Assert.That(BotPowerPlanner.PaymentLoss(owner.Kingdom, owned, Enumerable.Repeat(art, 4)),
            Is.EqualTo(float.PositiveInfinity));
        CollectionAssert.AreEqual(before, owned);
        CollectionAssert.AreEqual(new[] { art, art }, payment);
    }

    [Test]
    public void PaymentPlanUsesExactCheapMultiplicityAndPreservesExistingSelection()
    {
        Equip(owner, typeof(Magic), 2);
        Goals(owner, art, 3);
        owner.Virtues.AddRange(new[] { art, nature, nature });
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        CollectionAssert.AreEqual(new[] { nature, nature }, plan.Use.Payment);
        Assert.That(plan.Cost, Is.EqualTo(30));
        var use = Use(owner);
        use.Payment.Add(art);
        plan = BotPowerPlanner.Plan(owner, new PowerDecisionContext(PowerDecisionKind.Payment, owner, use));
        CollectionAssert.AreEqual(new[] { art, nature }, plan.Use.Payment);
        CollectionAssert.AreEqual(new[] { art }, use.Payment);
        CollectionAssert.AreEqual(new[] { art, nature, nature }, owner.Virtues);
    }

    [Test]
    public void IdentityPaymentPreservesPlannedEnergyInsteadOfCancellingAndReopening()
    {
        owner.Kingdom = Catalog().kingdoms.Single(kingdom => kingdom.power is IdentitySurfing);
        other.Kingdom = Catalog().kingdoms.Single(kingdom => kingdom.power is TimePower);
        other.kingdomRevealed = true;
        Virtue energy = Asset<Virtue>();
        energy.type = VirtueType.Energy;
        Virtue security = Asset<Virtue>();
        security.type = VirtueType.Security;
        ForgeManager.Instance.AllVirtues.AddRange(new[] { energy, security });
        owner.Virtues.AddRange(new[] { energy, security, security, security });
        SetField(TurnManager.Instance, "activePlayer", owner);
        BotPowerPlan original = BotPowerPlanner.Plan(owner);
        Assert.That(original.Score, Is.GreaterThan(0));
        CollectionAssert.AreEqual(new[] { security, security, security }, original.Use.Payment);
        manager.ActivatePower(owner.Kingdom.power);
        Assert.That(manager.Choices.First(choice => choice.Option.Kind == PowerOptionKind.PaymentAdd).Option.Virtue,
            Is.SameAs(energy), "The regression requires the equally cheap but strategically wrong virtue to appear first.");
        for (int selected = 0; selected < 3; selected++)
        {
            Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Payment));
            Assert.That(manager.DecisionContext.Use.Payment.Count, Is.EqualTo(selected));
            BotDecision decision = BotPowerPlanner.Analyze(owner);
            Assert.That(decision.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.PaymentAdd));
            Assert.That(decision.Chosen.Choice.Option.Virtue, Is.SameAs(security));
            BotPowerPlanner.Execute(decision);
            Assert.That(BotPowerPlanner.Plan(owner, manager.DecisionContext).Score, Is.GreaterThan(0),
                "Every payment prefix must preserve the original beneficial kingdom exchange.");
        }
        CollectionAssert.AreEqual(new[] { security, security, security }, manager.DecisionContext.Use.Payment);
        BotDecision next = BotPowerPlanner.Analyze(owner);
        Assert.That(next.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Continue));
        BotPowerPlanner.Execute(next);
        Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Target));
        next = BotPowerPlanner.Analyze(owner);
        Assert.That(next.Chosen.Choice.Option.Player, Is.SameAs(other));
        BotPowerPlanner.Execute(next);
        Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Confirm));
        next = BotPowerPlanner.Analyze(owner);
        Assert.That(next.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Use));
        BotPowerPlanner.Execute(next);
        Assert.That(manager.IsBusy, Is.False, "The planned payment must finish the action rather than cancel and retry.");
        CollectionAssert.AreEqual(new[] { energy }, owner.Virtues);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(other));
    }

    [Test]
    public void UnaffordableAndRevealedHiddenOnlyPowersAreNotProposed()
    {
        Power power = Equip(owner, typeof(Abundance), 1);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
        owner.Virtues.Add(nature);
        power.onlyWhileHidden = true;
        owner.kingdomRevealed = true;
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
        owner.kingdomRevealed = false;
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.GreaterThan(0));
    }

    [TestCase(typeof(Abundance))]
    [TestCase(typeof(BlessingOfPlenty))]
    [TestCase(typeof(Invisibility))]
    [TestCase(typeof(Magic))]
    [TestCase(typeof(TimePower))]
    [TestCase(typeof(Witchcraft))]
    public void UsefulBoardSituationsHavePositivePlansWithoutSpending(Type type)
    {
        Equip(owner, type, 0);
        Goals(owner, art, 3);
        Piece(owner, 0, ResourceType.Fire);
        Piece(owner, 5, ResourceType.Water);
        Piece(other, 0, ResourceType.Water);
        BotPowerPlan first = BotPowerPlanner.Plan(owner);
        BotPowerPlan repeat = BotPowerPlanner.Plan(owner);
        Assert.That(first.Score, Is.GreaterThan(0), type.Name);
        Assert.That(repeat.Score, Is.EqualTo(first.Score));
        Assert.That(repeat.Use.Target, Is.SameAs(first.Use.Target));
        Assert.That(owner.Virtues, Is.Empty);
        Assert.That(owner.kingdomRevealed, Is.False);
        Assert.That(PowerRules.Resources(owner).Count, Is.EqualTo(2));
    }

    [Test]
    public void KnowledgeValuesUnknownKingdomButPassesAlreadyKnownKingdomAtActualCost()
    {
        owner.Kingdom = Catalog().kingdoms.Single(item => item.power is InfiniteKnowledge);
        owner.Virtues.Add(art);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.GreaterThan(0));
        owner.knownKingdoms.Add(other.Kingdom);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThan(0));
    }

    [Test]
    public void TransformAtActualCostTradesOnlySurplusAndSeparatesActivationPayment()
    {
        Kingdom original = Catalog().kingdoms.Single(item => item.power is TransformPower);
        Equip(owner, typeof(TransformPower), original.power.virtueCost);
        Goals(owner, art, 2);
        owner.Virtues.AddRange(Enumerable.Repeat(nature, original.power.virtueCost + 2));
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        Assert.That(plan.Score, Is.GreaterThan(0));
        Assert.That(plan.Use.Payment.Count, Is.EqualTo(original.power.virtueCost));
        Assert.That(plan.Use.Exchange.Count, Is.EqualTo(2));
        Assert.That(plan.Use.ChosenVirtue, Is.SameAs(art));
        Assert.That(PowerRules.CanPay(owner.Virtues, plan.Use.Payment.Concat(plan.Use.Exchange).ToList(), 6), Is.True);
        Assert.That(manager.Validate(plan.Use, out string error), Is.True, error);
    }

    [Test]
    public void TransformCapsExchangeToActualReceiveStockInsteadOfPlanningAnInvalidTrade()
    {
        int cost = Catalog().kingdoms.Single(item => item.power is TransformPower).power.virtueCost;
        Equip(owner, typeof(TransformPower), cost);
        Goals(owner, art, 5);
        owner.Virtues.AddRange(Enumerable.Repeat(nature, cost + 4));
        other.Virtues.AddRange(Enumerable.Repeat(art, 11));
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        Assert.That(plan.Score, Is.GreaterThan(0));
        Assert.That(plan.Use.ChosenVirtue, Is.SameAs(art));
        Assert.That(plan.Use.Exchange.Count, Is.EqualTo(1));
        Assert.That(manager.Validate(plan.Use, out string error), Is.True, error);
        other.Virtues.Add(art);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
        Assert.That(owner.Virtues.Count, Is.EqualTo(cost + 4));
    }

    [Test]
    public void ImaginationTakesWinningVisibleVirtueButDoesNotReadHiddenInventory()
    {
        Equip(owner, typeof(Imagination), 3);
        Goals(owner, art, 1);
        owner.Virtues.AddRange(Enumerable.Repeat(nature, 3));
        other.Virtues.Add(art);
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        Assert.That(plan.Score, Is.GreaterThan(0));
        Assert.That(plan.Use.ChosenVirtue, Is.SameAs(art));
        other.virtuesHidden = true;
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
    }

    [Test]
    public void IdentitySurfingCanPreferKnownWinningGoalsAtActualCost()
    {
        int cost = Catalog().kingdoms.Single(item => item.power is IdentitySurfing).power.virtueCost;
        Equip(owner, typeof(IdentitySurfing), cost);
        owner.Kingdom.virtuesForWin = new[]
        {
            new Kingdom.VirtuesForCost { virtues = art, NumberofVirtues = 8 },
            new Kingdom.VirtuesForCost { virtues = nature, NumberofVirtues = 3 }
        };
        Goals(other, nature, 3);
        other.kingdomRevealed = true;
        owner.Virtues.AddRange(Enumerable.Repeat(art, cost));
        owner.Virtues.AddRange(Enumerable.Repeat(nature, 3));
        BotPowerPlan plan = BotPowerPlanner.Plan(owner);
        Assert.That(plan.Score, Is.GreaterThan(0));
        Assert.That(plan.Use.Target, Is.SameAs(other));
        Assert.That(plan.Use.Payment.Count, Is.EqualTo(cost));
        Assert.That(manager.Validate(plan.Use, out string error), Is.True, error);
    }

    [Test]
    public void CelestialDomeUsesForOwnedAttackLossButPassesAnEmptyBoardAtActualCost()
    {
        int cost = Catalog().kingdoms.Single(item => item.power is CelestialDome).power.virtueCost;
        Equip(owner, typeof(CelestialDome), cost);
        owner.Virtues.AddRange(Enumerable.Repeat(nature, cost));
        var context = new PowerDecisionContext(PowerDecisionKind.Reaction, owner) { IsAttack = true };
        Show(context, Option(PowerOptionKind.Use), Option(PowerOptionKind.Pass));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Pass));
        Piece(owner, 0, ResourceType.Fire);
        Piece(owner, 1, ResourceType.Water);
        Show(context, Option(PowerOptionKind.Pass), Option(PowerOptionKind.Use));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Use));
    }

    [Test]
    public void EmptyCopySourceAndFullStealDestinationAreNotBeneficial()
    {
        Equip(owner, typeof(BlessingOfPlenty), 0);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
        Equip(owner, typeof(Invisibility), 0);
        Piece(other, 0, ResourceType.Water);
        foreach (Slot slot in owner.Board.Slots)
            Piece(owner, slot.Index, ResourceType.Fire);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
    }

    [Test]
    public void MagicDoesNotChainDuringEitherGrantedActionIncludingTheZeroCounterFinalAction()
    {
        Equip(owner, typeof(Magic), 0);
        SetField(TurnManager.Instance, "activePlayer", owner);
        manager.BeginNormalTurn(owner);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.GreaterThan(0));
        int completed = 0;
        manager.GrantTwoActions();
        Assert.That(manager.AdditionalActions, Is.EqualTo(2));
        Assert.That(manager.MagicActionsActive, Is.True);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
        for (int remaining = 1; remaining >= 0; remaining--)
        {
            manager.CompleteAction(owner, () => completed++);
            Assert.That(completed, Is.Zero);
            Assert.That(manager.AdditionalActions, Is.EqualTo(remaining));
            Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Notice));
            BotPowerPlanner.Execute(BotPowerPlanner.Analyze(owner));
            Assert.That(manager.IsBusy, Is.False);
            Assert.That(manager.MagicActionsActive, Is.True,
                "The final granted action is still active even when no additional actions remain queued.");
            Assert.That(BotPowerPlanner.Plan(owner).Score, Is.LessThanOrEqualTo(0));
            Assert.That(manager.CanUse(owner), Is.True, "This is a bot utility rule, not a human power prohibition.");
        }
        manager.CompleteAction(owner, () => completed++);
        Assert.That(completed, Is.EqualTo(1));
        Assert.That(manager.MagicActionsActive, Is.False);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.GreaterThan(0));
        manager.GrantTwoActions();
        manager.BeginNormalTurn(owner);
        Assert.That(manager.MagicActionsActive, Is.False);
        Assert.That(manager.AdditionalActions, Is.Zero);
    }

    [Test]
    public void MagicObservationFlagDoesNotPreventAHumanFromChainingThePower()
    {
        Power power = Equip(owner, typeof(Magic), 0);
        owner.hasFinishedSettingUp = true;
        SetField(TurnManager.Instance, "activePlayer", owner);
        manager.BeginNormalTurn(owner);
        manager.GrantTwoActions();
        manager.ActivatePower(power);
        Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Payment));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Cancel));
        PowerChoice next = manager.Choices.Single(choice => choice.Option.Kind == PowerOptionKind.Continue);
        Assert.That(manager.TrySelectChoice(owner, manager.ChoiceRevision, next), Is.True);
        Assert.That(manager.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Confirm));
        PowerChoice commit = manager.Choices.Single(choice => choice.Option.Kind == PowerOptionKind.Use);
        Assert.That(manager.TrySelectChoice(owner, manager.ChoiceRevision, commit), Is.True);
        Assert.That(manager.MagicActionsActive, Is.True);
        Assert.That(manager.AdditionalActions, Is.EqualTo(3),
            "The existing human callback grants two more actions, then consumes the current action.");
    }

    [TestCase(typeof(IdentitySurfing))]
    [TestCase(typeof(InfiniteKnowledge))]
    [TestCase(typeof(Invisibility))]
    [TestCase(typeof(Rain))]
    [TestCase(typeof(Manipulation))]
    public void HiddenOpponentGoalsDoNotChangePowerScoreOrTarget(Type type)
    {
        Equip(owner, type, 0);
        Goals(owner, art, 3);
        other.virtuesHidden = true;
        other.kingdomRevealed = false;
        Piece(other, 0, ResourceType.Fire);
        Piece(other, 1, ResourceType.Water);
        var context = new PowerDecisionContext(PowerDecisionKind.Reaction, owner, Use(owner));
        context.Use.Target = other;
        Goals(other, art, 1);
        BotPowerPlan before = BotPowerPlanner.Plan(owner, context);
        Goals(other, nature, 11);
        other.Virtues.Add(nature);
        BotPowerPlan after = BotPowerPlanner.Plan(owner, context);
        Assert.That(after.Score, Is.EqualTo(before.Score), "Neither hidden goals nor hidden inventory is a scoring input.");
        Assert.That(after.Use.Target, Is.SameAs(before.Use.Target));
        Assert.That(after.Use.ResourceCount, Is.EqualTo(before.Use.ResourceCount));
        Assert.That(after.Use.RemoveResources, Is.EqualTo(before.Use.RemoveResources));
    }

    [Test]
    public void ControlledPlayerGoalsAndVirtuesAreVisibilityGated()
    {
        Goals(other, art, 1);
        other.virtuesHidden = true;
        var context = new PowerDecisionContext(PowerDecisionKind.Payment, owner, Use(other));
        float before = BotPowerPlanner.Plan(other, context).Score;
        Assert.That(before, Is.LessThanOrEqualTo(0));
        Assert.That(BotPowerPlanner.Recipes(other, owner).All(recipe => recipe.Needed == 0), Is.True);
        Goals(other, nature, 8);
        other.Virtues.Add(nature);
        Assert.That(BotPowerPlanner.Plan(other, context).Score, Is.EqualTo(before));
        other.kingdomRevealed = true;
        Assert.That(BotPowerPlanner.Recipes(other, owner).All(recipe => recipe.Needed == 0), Is.True);
        other.virtuesHidden = false;
        Assert.That(BotPowerPlanner.Recipes(other, owner).Single(recipe => recipe.Virtue == nature.type).Needed,
            Is.EqualTo(7));
    }

    [Test]
    public void ManipulationValuesPublicForgeOpportunityWithoutReadingControlledGoals()
    {
        int cost = Catalog().kingdoms.Single(item => item.power is Manipulation).power.virtueCost;
        Equip(owner, typeof(Manipulation), cost);
        owner.Virtues.AddRange(Enumerable.Repeat(nature, cost));
        other.virtuesHidden = true;
        Goals(other, art, 1);
        Piece(other, 0, ResourceType.Fire);
        Piece(other, 1, ResourceType.Water);
        BoardManager.slots.Clear();
        BoardManager.slots.AddRange(other.Board.Slots);
        var context = new PowerDecisionContext(PowerDecisionKind.Reaction, owner) { Target = other };
        BotPowerPlan before = BotPowerPlanner.Plan(owner, context);
        Assert.That(before.Score, Is.GreaterThan(0), "A publicly forgeable pair is an opportunity even with unknown goals.");
        Goals(other, nature, 9);
        other.Virtues.Add(nature);
        Assert.That(BotPowerPlanner.Plan(owner, context).Score, Is.EqualTo(before.Score));
        Assert.That(BotPowerPlanner.Recipes(other, owner).All(recipe => recipe.Needed == 0), Is.True);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SnapshotRetractionValueDoesNotExposeHiddenBeforeOrAfterGoals(bool hideVirtues)
    {
        Equip(owner, typeof(Retraction), 0);
        Goals(owner, art, 3);
        other.virtuesHidden = hideVirtues;
        Goals(other, art, 1);
        manager.BeginNormalTurn(other);
        other.Virtues.Add(art);
        float before = BotPowerPlanner.Plan(owner).Score;
        Goals(other, nature, 8);
        other.Virtues.Clear();
        other.Virtues.Add(nature);
        Assert.That(BotPowerPlanner.Plan(owner).Score, Is.EqualTo(before));
        Assert.That(manager.ActionSnapshot, Is.Not.Null);
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void SnapshotObservationRequiresVisibilityBothBeforeAndAfterTheAction(bool before, bool after)
    {
        other.kingdomRevealed = before;
        other.virtuesHidden = !before;
        other.Virtues.Add(art);
        Piece(other, 0, ResourceType.Fire);
        manager.BeginNormalTurn(other);
        other.kingdomRevealed = after;
        other.virtuesHidden = !after;
        other.Virtues.Add(nature);
        Piece(other, 1, ResourceType.Water);
        SnapshotObservation observation = manager.ObserveBeforeAction(other, owner);
        Assert.That(observation, Is.Not.Null);
        Assert.That(observation.Board.Count, Is.EqualTo(1));
        Assert.That(observation.Board[0], Is.EqualTo(ResourceType.Fire));
        Assert.That(observation.Board.ContainsKey(1), Is.False, "Later board changes must not alter the captured observation.");
        if (before && after)
        {
            Assert.That(observation.Kingdom, Is.SameAs(other.Kingdom));
            CollectionAssert.AreEqual(new[] { VirtueType.Art }, observation.Virtues);
        }
        else
        {
            Assert.That(observation.Kingdom, Is.Null);
            Assert.That(observation.Virtues, Is.Null, "Neither later disclosure nor cached former visibility exposes hidden inventory.");
        }
        SnapshotObservation own = manager.ObserveBeforeAction(other, other);
        Assert.That(own.Kingdom, Is.SameAs(other.Kingdom));
        CollectionAssert.AreEqual(new[] { VirtueType.Art }, own.Virtues);
        Assert.Throws<NotSupportedException>(() => ((IList<VirtueType>)own.Virtues).Add(VirtueType.Nature));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<int, ResourceType>)own.Board).Add(2, ResourceType.Earth));
    }

    [TestCase(PowerDecisionKind.Reaction)]
    [TestCase(PowerDecisionKind.Confirm)]
    public void UseVersusPassAccountsForCostAndUsesExplicitOwner(PowerDecisionKind stage)
    {
        Equip(owner, typeof(Magic), 0);
        var context = new PowerDecisionContext(stage, owner, Use(owner));
        Show(context, Option(PowerOptionKind.Pass), Option(PowerOptionKind.Use));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Use));
        Assert.Throws<InvalidOperationException>(() => BotPowerPlanner.Analyze(other));
        Equip(owner, typeof(Magic), 1);
        Goals(owner, art, 1);
        owner.Virtues.Add(art);
        context.Use = Use(owner);
        context.Use.Payment.Add(art);
        Show(context, Option(PowerOptionKind.Use), Option(PowerOptionKind.Pass));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Pass));
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(other));
    }

    [Test]
    public void NecklaceUnderstandsNestedCancellationRatherThanAlwaysBlockingPowers()
    {
        Equip(owner, typeof(KingsNecklace), 0);
        var original = Use(other);
        var context = new PowerDecisionContext(PowerDecisionKind.Reaction, owner, Use(owner));
        context.Use.Trigger = original;
        Show(context, Option(PowerOptionKind.Pass), Option(PowerOptionKind.Use));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Use));
        Power necklace = Equip(other, typeof(KingsNecklace), 0);
        context.Use.Trigger = new PowerUse { Caster = other, Power = necklace, Trigger = original };
        Show(context, Option(PowerOptionKind.Use), Option(PowerOptionKind.Pass));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Pass));
    }

    [Test]
    public void TypedPaymentMovesForwardWithoutUnselectingOrOverpaying()
    {
        Equip(owner, typeof(Magic), 1);
        owner.Virtues.AddRange(new[] { art, nature });
        Goals(owner, art, 3);
        var context = new PowerDecisionContext(PowerDecisionKind.Payment, owner, Use(owner));
        Show(context, Option(PowerOptionKind.PaymentRemove, art), Option(PowerOptionKind.Back),
            Option(PowerOptionKind.Cancel), Option(PowerOptionKind.PaymentAdd, art), Option(PowerOptionKind.PaymentAdd, nature));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Virtue, Is.SameAs(nature));
        context.Use.Payment.Add(nature);
        Show(context, Option(PowerOptionKind.PaymentRemove, nature), Option(PowerOptionKind.Continue), Option(PowerOptionKind.Cancel));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Continue));
    }

    [Test]
    public void TypedTargetExchangeVirtueAndRainOperationHaveForwardPolicies()
    {
        Equip(owner, typeof(InfiniteKnowledge), 0);
        Show(new PowerDecisionContext(PowerDecisionKind.Target, owner, Use(owner)),
            Option(PowerOptionKind.Back), new PowerOption(PowerOptionKind.Target) { Player = other });
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Player, Is.SameAs(other));
        Equip(owner, typeof(TransformPower), 0);
        Goals(owner, art, 2);
        owner.Virtues.AddRange(new[] { nature, nature });
        var use = Use(owner);
        Show(new PowerDecisionContext(PowerDecisionKind.Exchange, owner, use),
            Option(PowerOptionKind.Back), Option(PowerOptionKind.ExchangeAdd, nature), Option(PowerOptionKind.Continue));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.ExchangeAdd));
        use.Exchange.AddRange(new[] { nature, nature });
        Show(new PowerDecisionContext(PowerDecisionKind.Exchange, owner, use),
            Option(PowerOptionKind.ExchangeRemove, nature), Option(PowerOptionKind.Continue));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Continue));
        Show(new PowerDecisionContext(PowerDecisionKind.Virtue, owner, use),
            Option(PowerOptionKind.Virtue, nature), Option(PowerOptionKind.Virtue, art), Option(PowerOptionKind.Back));
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Virtue, Is.SameAs(art));
        Equip(owner, typeof(Rain), 0);
        Show(new PowerDecisionContext(PowerDecisionKind.RainOperation, owner, Use(owner)),
            new PowerOption(PowerOptionKind.RainOperation) { Count = 1, Remove = true },
            new PowerOption(PowerOptionKind.RainOperation) { Count = 3, Remove = false }, Option(PowerOptionKind.Back));
        PowerOption chosen = BotPowerPlanner.Analyze(owner).Chosen.Choice.Option;
        Assert.That(chosen.Count, Is.EqualTo(3));
        Assert.That(chosen.Remove, Is.False);
    }

    [Test]
    public void TypedEffectsChooseResourcesSlotsStealsAndLeastHarmfulRemoval()
    {
        Goals(owner, art, 3);
        Resource fire = Piece(owner, 0, ResourceType.Fire);
        Resource water = Piece(owner, 1, ResourceType.Water);
        Resource surplus = Piece(owner, 7, ResourceType.Earth);
        Show(new PowerDecisionContext(PowerDecisionKind.AddResource, owner),
            new PowerOption(PowerOptionKind.ResourceType) { ResourceType = ResourceType.Water });
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.ResourceType, Is.EqualTo(ResourceType.Water));
        Show(new PowerDecisionContext(PowerDecisionKind.EmptySlot, owner) { ResourceType = ResourceType.Water },
            new PowerOption(PowerOptionKind.Slot) { Slot = owner.Board.Slots[2] });
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Slot.board.player, Is.SameAs(owner));
        Resource stolen = Piece(other, 0, ResourceType.Water);
        Show(new PowerDecisionContext(PowerDecisionKind.StealResource, owner) { Target = other },
            new PowerOption(PowerOptionKind.Resource) { Resource = stolen });
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Resource, Is.SameAs(stolen));
        Show(new PowerDecisionContext(PowerDecisionKind.RemoveResource, owner),
            new PowerOption(PowerOptionKind.Resource) { Resource = fire },
            new PowerOption(PowerOptionKind.Resource) { Resource = water },
            new PowerOption(PowerOptionKind.Resource) { Resource = surplus });
        Assert.That(BotPowerPlanner.Analyze(owner).Chosen.Choice.Option.Resource, Is.SameAs(surplus));
    }

    [Test]
    public void WitchcraftSelectsStrictImprovementAndFinishesAtLocalOptimum()
    {
        Equip(owner, typeof(Witchcraft), 0);
        Goals(owner, art, 3);
        Piece(owner, 0, ResourceType.Fire);
        Piece(owner, 5, ResourceType.Water);
        int finished = 0;
        var use = Use(owner);
        use.Complete = () => finished++;
        PowerEffects.Rearrange(use);
        int moves = 0;
        for (int step = 0; step < 20 && finished == 0; step++)
        {
            BotDecision decision = BotPowerPlanner.Analyze(owner);
            Assert.That(decision.Chosen.Choice.Option.Kind,
                Is.EqualTo(PowerOptionKind.Resource).Or.EqualTo(PowerOptionKind.Slot).Or.EqualTo(PowerOptionKind.Finish));
            if (manager.DecisionContext.Kind == PowerDecisionKind.RearrangeDestination)
            {
                float before = BotPowerPlanner.BoardValue(owner, owner);
                BotPowerPlanner.Execute(decision);
                Assert.That(BotPowerPlanner.BoardValue(owner, owner), Is.GreaterThan(before));
                moves++;
            }
            else
                BotPowerPlanner.Execute(decision);
        }
        Assert.That(moves, Is.GreaterThan(0));
        Assert.That(finished, Is.EqualTo(1), "Witchcraft must not cycle or choose Back indefinitely.");
    }

    [Test]
    public void NoticeContinuesButManualInspectionAndUnknownMenusFailClosed()
    {
        int invoked = 0;
        manager.Choose("Translated notice", new List<PowerChoice>
        {
            new PowerChoice("Unrelated display text", () => invoked++, option: Option(PowerOptionKind.Continue))
        }, new PowerDecisionContext(PowerDecisionKind.Notice, owner));
        BotPowerPlanner.Execute(BotPowerPlanner.Analyze(owner));
        Assert.That(invoked, Is.EqualTo(1));
        foreach (PowerDecisionKind kind in new[] { PowerDecisionKind.Unknown, PowerDecisionKind.ManualInspection })
        {
            Show(new PowerDecisionContext(kind, owner), Option(PowerOptionKind.Continue));
            Assert.Throws<InvalidOperationException>(() => BotPowerPlanner.Analyze(owner));
        }
        Show(new PowerDecisionContext(PowerDecisionKind.Notice, owner), Option(PowerOptionKind.Unknown), Option(PowerOptionKind.Continue));
        Assert.Throws<InvalidOperationException>(() => BotPowerPlanner.Analyze(owner));
    }

    [Test]
    public void ChoiceSubmissionRejectsNonOwnerStaleRevisionDisabledAndDuplicates()
    {
        int invoked = 0;
        var context = new PowerDecisionContext(PowerDecisionKind.Notice, owner);
        manager.Choose("Opaque", new List<PowerChoice>
        {
            new PowerChoice("A", () => invoked++, option: Option(PowerOptionKind.Continue)),
            new PowerChoice("B", () => invoked++, false, Option(PowerOptionKind.Continue))
        }, context);
        PowerChoice choice = manager.Choices[0], disabled = manager.Choices[1];
        int revision = manager.ChoiceRevision;
        BotDecision stale = BotPowerPlanner.Analyze(owner);
        Assert.That(manager.TrySelectChoice(other, revision, choice), Is.False);
        Assert.That(manager.TrySelectChoice(owner, revision - 1, choice), Is.False);
        Assert.That(manager.TrySelectChoice(owner, revision, disabled), Is.False);
        Assert.That(invoked, Is.Zero);
        Assert.That(manager.TrySelectChoice(owner, revision, choice), Is.True);
        Assert.That(manager.TrySelectChoice(owner, revision, choice), Is.False);
        Show(context, Option(PowerOptionKind.Continue));
        Assert.Throws<InvalidOperationException>(() => BotPowerPlanner.Execute(stale));
        Assert.That(invoked, Is.EqualTo(1));
    }

    [Test]
    public void ChangedOwnedInventoryInvalidatesAnalysisBeforeCallback()
    {
        int invoked = 0;
        manager.Choose("Notice", new List<PowerChoice>
        {
            new PowerChoice("Continue", () => invoked++, option: Option(PowerOptionKind.Continue))
        }, new PowerDecisionContext(PowerDecisionKind.Notice, owner));
        BotDecision decision = BotPowerPlanner.Analyze(owner);
        owner.Virtues.Add(art);
        Assert.Throws<InvalidOperationException>(() => BotPowerPlanner.Execute(decision));
        Assert.That(invoked, Is.Zero);
        BotPowerPlanner.Execute(BotPowerPlanner.Analyze(owner));
        Assert.That(invoked, Is.EqualTo(1));
    }

    [Test]
    public void HiddenOpponentEditsDoNotInvalidateOtherwiseIdenticalNoticeAnalysis()
    {
        int invoked = 0;
        other.virtuesHidden = true;
        other.kingdomRevealed = false;
        manager.Choose("Notice", new List<PowerChoice>
        {
            new PowerChoice("Continue", () => invoked++, option: Option(PowerOptionKind.Continue))
        }, new PowerDecisionContext(PowerDecisionKind.Notice, owner));
        BotDecision decision = BotPowerPlanner.Analyze(owner);
        Goals(other, nature, 9);
        other.Virtues.Add(nature);
        BotPowerPlanner.Execute(decision);
        Assert.That(invoked, Is.EqualTo(1), "The decision fingerprint must not encode another player's secrets.");
    }

    private static PowerOption Option(PowerOptionKind kind, Virtue virtue = null) =>
        new PowerOption(kind) { Virtue = virtue };

    private void Show(PowerDecisionContext context, params PowerOption[] options)
    {
        manager.Choose("Labels are deliberately not a protocol", options.Select((option, index) =>
            new PowerChoice("Opaque " + index, () => { }, option: option)).ToList(), context);
    }

    private static KingdomCatalog Catalog() => AssetDatabase.LoadAssetAtPath<KingdomCatalog>(
        "Assets/Busara/ScriptableObjects/Kingdoms/KingdomCatalog.asset");

    private PowerUse Use(Player player) => new PowerUse { Caster = player, Power = player.Kingdom.power };

    private void Goals(Player player, Virtue virtue, int count) =>
        player.Kingdom.virtuesForWin = new[]
        {
            new Kingdom.VirtuesForCost { virtues = virtue, NumberofVirtues = count }
        };

    private T Component<T>(string name) where T : Component
    {
        var instance = new GameObject(name);
        instance.transform.SetParent(root.transform);
        return instance.AddComponent<T>();
    }

    private T Asset<T>() where T : ScriptableObject
    {
        T asset = ScriptableObject.CreateInstance<T>();
        assets.Add(asset);
        return asset;
    }

    private Power Equip(Player player, Type type, int cost)
    {
        var power = (Power)ScriptableObject.CreateInstance(type);
        assets.Add(power);
        power.powerName = type.Name;
        power.virtueCost = cost;
        power.timing = PowerTiming.OwnTurn;
        player.Kingdom = Asset<Kingdom>();
        player.Kingdom.power = power;
        Goals(player, art, 3);
        return power;
    }

    private Player MakePlayer(string name)
    {
        Player player = Component<Player>(name);
        player.Name = name;
        player.Board = Component<Board>(name + " board");
        player.Board.player = player;
        player.Board.Slots = new List<Slot>();
        for (int i = 0; i < 8; i++)
        {
            Slot slot = Component<Slot>(name + " slot " + i);
            slot.board = player.Board;
            slot.Index = i;
            slot.transform.SetParent(player.Board.transform);
            player.Board.Slots.Add(slot);
        }
        PlayerManager.Instance.Players.Add(player);
        BoardManager.Instance.gameBoards.Add(player.Board);
        return player;
    }

    private Resource Piece(Player player, int index, ResourceType type)
    {
        Resource resource = Component<Resource>(type.ToString());
        resource.resourceType = type;
        Board.PlaceResource(resource, player.Board.Slots[index]);
        return resource;
    }

    private static void SetField(object instance, string name, object value)
    {
        FieldInfo field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(instance, value);
    }
}
