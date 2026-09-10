using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class BotPowerSceneTests
{
    private const string BackupKey = "Busara.BotPowerSceneTests.SceneBackup";
    private const string BackgroundKey = "Busara.BotPowerSceneTests.Background";
    private string phase;

    [UnityTest]
    public IEnumerator AbundanceBotActivatesOnItsFirstTurnWithoutSpendingOrDrawing()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return FirstTurnAbundanceScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator FirstTurnAbundanceScenario()
    {
        yield return AwaitSetup();
        Start(new[] { typeof(Abundance), typeof(InfiniteKnowledge) }, new[] { true, false });
        Player caster = Players[0], human = Players[1];
        BotPlayerController bot = caster.GetComponent<BotPlayerController>();
        var originals = PowerRules.Resources(caster).ToDictionary(resource => resource.slot, resource => resource);
        Card[] deck = DeckManager.Instance.Cards.ToArray();
        int cardCount = DeckManager.Instance.CardCount;
        int added = Players.Count;
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(caster));
        Assert.That(caster.kingdomRevealed, Is.False);
        Assert.That(caster.Virtues, Is.Empty);
        Assert.That(HeuristicBot.Analyze(caster).Chosen.Kind, Is.EqualTo(BotActionKind.UsePower));
        phase = "First-turn Abundance through automatic payment/confirmation/resource choices";
        bot.SetPaused(false);
        try
        {
            float deadline = Time.realtimeSinceStartup + 15;
            while (TurnManager.Instance.ActivePlayer == caster && Time.realtimeSinceStartup < deadline && !bot.Failed)
            {
                AssertBotsHealthy();
                if (PowerManager.Instance.IsBusy)
                    Assert.That(PowerManager.Instance.DecisionContext.Owner, Is.SameAs(caster));
                yield return null;
            }
            PauseAll();
            AssertBotsHealthy();
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
            Assert.That(caster.kingdomRevealed, Is.True);
            Assert.That(caster.Virtues, Is.Empty);
            Assert.That(PowerRules.Resources(caster).Count, Is.EqualTo(originals.Count + added));
            foreach (var original in originals)
                Assert.That(original.Key.resource, Is.SameAs(original.Value), "Abundance must preserve existing pieces.");
            CollectionAssert.AreEqual(deck, DeckManager.Instance.Cards);
            Assert.That(DeckManager.Instance.CardCount, Is.EqualTo(cardCount));
            Assert.That(PowerManager.Instance.IsBusy, Is.False);
            Assert.That(PowerManager.Instance.CanUse(caster), Is.False, "The free hidden-only activation cannot repeat.");
            Assert.That(bot.ExecutedSteps, Is.EqualTo(3 + 2 * added));
            int steps = bot.ExecutedSteps;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
            Assert.That(bot.ExecutedSteps, Is.EqualTo(steps));
            Assert.That(human.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
        }
        finally
        {
            PauseAll();
        }
    }

    [UnityTest]
    public IEnumerator RetractionBotUsesAfterOwnLossWhileAnotherPlayerIsActive()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return RetractionScenario(0);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RetractionBotUsesAgainstKnownOpponentGoalAdvance()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return RetractionScenario(1);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RetractionBotPassesAnInexpensiveOpponentSurplusGain()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return RetractionScenario(2);
        yield return new ExitPlayMode();
    }

    private IEnumerator RetractionScenario(int situation)
    {
        yield return AwaitSetup();
        Start(new[] { typeof(InfiniteKnowledge), typeof(Retraction) }, new[] { false, true });
        Player active = Players[0], reactor = Players[1];
        BotPlayerController bot = reactor.GetComponent<BotPlayerController>();
        Virtue goal = Virtue(VirtueType.Art), surplus = Virtue(VirtueType.Nature);
        var clones = new List<Kingdom>();
        var originals = Players.ToDictionary(player => player, player => player.Kingdom);
        try
        {
            foreach (Player player in Players)
            {
                ClearBoard(player);
                BusaraPlaytestTools.AddResources(player, ResourceType.Earth, 1);
                clones.Add(CloneGoals(player, goal, 3));
                player.kingdomRevealed = true;
            }
            reactor.Virtues.Add(goal);
            reactor.Virtues.AddRange(Enumerable.Repeat(surplus, reactor.Kingdom.power.virtueCost));
            PowerManager.Instance.RefreshPlaytestState();
            AssertGameRunning();
            Assert.That(PowerManager.Instance.ActionSnapshot, Is.Not.Null);
            if (situation == 0)
            {
                reactor.Virtues.Remove(goal);
                PowerManager.Instance.GrantTwoActions();
                Assert.That(PowerManager.Instance.MagicActionsActive, Is.True);
            }
            else
                active.Virtues.Add(situation == 1 ? goal : surplus);
            TurnManager.Instance.CompleteTurn(active);
            PowerManager powers = PowerManager.Instance;
            Assert.That(powers.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Reaction));
            Assert.That(powers.DecisionContext.Owner, Is.SameAs(reactor));
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(active));
            Assert.That(HeuristicBot.BlockReason(reactor), Is.Empty);
            Assert.That(HeuristicBot.BlockReason(active), Is.Not.Empty);
            BotDecision decision = HeuristicBot.Analyze(reactor);
            Assert.That(decision.Chosen.Choice.Option.Kind,
                Is.EqualTo(situation == 2 ? PowerOptionKind.Pass : PowerOptionKind.Use));
            int revision = powers.ChoiceRevision;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(powers.ChoiceRevision, Is.EqualTo(revision), "Paused bots must leave their reaction untouched.");
            var stages = new HashSet<PowerDecisionKind>();
            phase = "Automatic Retraction offer/payment/confirmation/notice";
            bot.SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 15;
            while (TurnManager.Instance.ActivePlayer == active && Time.realtimeSinceStartup < deadline && !bot.Failed)
            {
                AssertGameRunning();
                if (powers.IsBusy)
                {
                    stages.Add(powers.DecisionContext.Kind);
                    Assert.That(powers.DecisionContext.Owner, Is.SameAs(reactor));
                    if (situation == 0 && powers.DecisionContext.Kind == PowerDecisionKind.Notice)
                    {
                        Assert.That(powers.MagicActionsActive, Is.False, "Undo must clear Magic before the next normal turn resets it.");
                        Assert.That(powers.AdditionalActions, Is.Zero);
                    }
                }
                yield return null;
            }
            bot.SetPaused(true);
            AssertGameRunning();
            Assert.That(bot.Failed, Is.False, bot.Status);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(reactor), "Reactions must advance the original turn exactly once.");
            Assert.That(powers.IsBusy, Is.False);
            Assert.That(stages, Does.Contain(PowerDecisionKind.Reaction));
            if (situation == 2)
            {
                Assert.That(bot.ExecutedSteps, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { surplus }, active.Virtues);
                Assert.That(reactor.Virtues.Count(item => item == surplus), Is.EqualTo(reactor.Kingdom.power.virtueCost));
            }
            else
            {
                Assert.That(stages, Does.Contain(PowerDecisionKind.Payment));
                Assert.That(stages, Does.Contain(PowerDecisionKind.Confirm));
                Assert.That(stages, Does.Contain(PowerDecisionKind.Notice));
                CollectionAssert.AreEqual(new[] { goal }, reactor.Virtues, "Undo restores the loss, then charges the exact activation cost.");
                Assert.That(active.Virtues, Is.Empty);
            }
            Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(decision));
            int steps = bot.ExecutedSteps;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(bot.ExecutedSteps, Is.EqualTo(steps));
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(reactor));
        }
        finally
        {
            bot.SetPaused(true);
            foreach (var original in originals)
                original.Key.Kingdom = original.Value;
            foreach (Kingdom clone in clones)
                Object.Destroy(clone);
        }
    }

    [UnityTest]
    public IEnumerator AllBotsResolveOwnPowerNestedNecklaceAndNoticeWithoutForcedPasses()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return NestedNecklaceScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator NestedNecklaceScenario()
    {
        yield return AwaitSetup();
        Start(new[] { typeof(Magic), typeof(KingsNecklace), typeof(InfiniteKnowledge) }, new[] { true, true, true });
        Player caster = Players[0], blocker = Players[1], counter = Players[2];
        Virtue goal = Virtue(VirtueType.Art), surplus = Virtue(VirtueType.Nature);
        var clones = new List<Kingdom>();
        var originals = Players.ToDictionary(player => player, player => player.Kingdom);
        PowerManager manager = PowerManager.Instance;
        Action onActivation = null, onResolution = null;
        try
        {
            foreach (Player player in Players)
            {
                ClearBoard(player);
                clones.Add(CloneGoals(player, goal, 3));
                player.kingdomRevealed = true;
            }
            counter.Kingdom.power = blocker.Kingdom.power;
            caster.Virtues.AddRange(Enumerable.Repeat(surplus, caster.Kingdom.power.virtueCost));
            blocker.Virtues.AddRange(Enumerable.Repeat(surplus, blocker.Kingdom.power.virtueCost));
            counter.Virtues.AddRange(Enumerable.Repeat(surplus, counter.Kingdom.power.virtueCost));
            PlacePair(caster, goal);
            BusaraPlaytestTools.AddResources(blocker, ResourceType.Earth, 1);
            BusaraPlaytestTools.AddResources(counter, ResourceType.Earth, 1);
            BusaraPlaytestTools.ForceNextCard(DeckManager.Instance.Cards.OfType<ResourceCard>().First());
            PowerManager.Instance.RefreshPlaytestState();
            AssertGameRunning();
            Assert.That(HeuristicBot.Analyze(caster).Chosen.Kind, Is.EqualTo(BotActionKind.UsePower));
            var phases = new HashSet<PowerDecisionKind>();
            bool nestedReaction = false, nestedPass = false, blockerUse = false, paidBeforeCancellation = false;
            PowerChoice usedChoice = null, passedChoice = null;
            int usedRevision = -1, passedRevision = -1, useCount = 0, passCount = 0;
            // These events run inside the consumed callback, before the next menu replaces its context.
            onActivation = () =>
            {
                PowerDecisionContext context = manager.DecisionContext;
                if (context == null || context.Kind != PowerDecisionKind.Reaction || context.Owner != blocker ||
                    !(context.Trigger?.Power is Magic))
                    return;
                BotDecision executed = blocker.GetComponent<BotPlayerController>().LastDecision;
                Assert.That(executed.Player, Is.SameAs(blocker));
                Assert.That(executed.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Use));
                Assert.That(manager.Choices, Does.Contain(executed.Chosen.Choice));
                usedChoice = executed.Chosen.Choice;
                usedRevision = manager.ChoiceRevision - 1;
                useCount++;
                blockerUse = true;
                phases.Add(context.Kind);
            };
            onResolution = () =>
            {
                PowerDecisionContext context = manager.DecisionContext;
                if (context == null)
                    return;
                phases.Add(context.Kind);
                if (context.Kind == PowerDecisionKind.Notice)
                    paidBeforeCancellation |= caster.Virtues.Count == 0 && blocker.Virtues.Count == 0;
                if (context.Kind != PowerDecisionKind.Reaction || context.Owner != counter ||
                    !(context.Trigger?.Power is KingsNecklace))
                    return;
                BotDecision executed = counter.GetComponent<BotPlayerController>().LastDecision;
                Assert.That(executed.Player, Is.SameAs(counter));
                Assert.That(executed.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Pass));
                Assert.That(manager.Choices, Does.Contain(executed.Chosen.Choice));
                passedChoice = executed.Chosen.Choice;
                passedRevision = manager.ChoiceRevision - 1;
                passCount++;
                nestedReaction = nestedPass = true;
            };
            manager.OnPowerActivated += onActivation;
            manager.OnStateChanged += onResolution;
            phase = "All-bot real own-turn activation and nested necklace reaction";
            foreach (Player player in Players)
                player.GetComponent<BotPlayerController>().SetPaused(false);
            float deadline = Time.realtimeSinceStartup + 25;
            while (TurnManager.Instance.ActivePlayer == caster && Time.realtimeSinceStartup < deadline)
            {
                AssertBotsHealthy();
                PowerManager powers = PowerManager.Instance;
                if (powers.IsBusy)
                {
                    PowerDecisionContext context = powers.DecisionContext;
                    Assert.That(context, Is.Not.Null);
                    phases.Add(context.Kind);
                    Assert.That(powers.Choices.All(choice => choice.Option != null &&
                        choice.Option.Kind != PowerOptionKind.Unknown), Is.True);
                    if (context.Kind == PowerDecisionKind.Notice)
                        paidBeforeCancellation |= caster.Virtues.Count == 0 && blocker.Virtues.Count == 0;
                }
                yield return null;
            }
            PauseAll();
            AssertBotsHealthy();
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(blocker));
            Assert.That(blockerUse, Is.True, "An adverse power must produce a scored Use, not blanket passing.");
            Assert.That(nestedReaction && nestedPass, Is.True, "Do not undo another player's helpful cancellation.");
            Assert.That(useCount, Is.EqualTo(1));
            Assert.That(passCount, Is.EqualTo(1));
            Assert.That(manager.TrySelectChoice(blocker, usedRevision, usedChoice), Is.False);
            Assert.That(manager.TrySelectChoice(counter, passedRevision, passedChoice), Is.False);
            Assert.That(paidBeforeCancellation, Is.True);
            Assert.That(phases, Does.Contain(PowerDecisionKind.Payment));
            Assert.That(phases, Does.Contain(PowerDecisionKind.Confirm));
            Assert.That(phases, Does.Contain(PowerDecisionKind.Reaction));
            Assert.That(phases, Does.Contain(PowerDecisionKind.Notice));
            Assert.That(caster.Virtues, Is.Empty);
            Assert.That(blocker.Virtues, Is.Empty);
            Assert.That(counter.Virtues.Count, Is.EqualTo(counter.Kingdom.power.virtueCost));
            Assert.That(PowerRules.Resources(caster).Count, Is.EqualTo(2), "The forge pair must not be consumed by an unrelated fallback action.");
            Assert.That(PowerManager.Instance.AdditionalActions, Is.Zero, "Cancelled Magic cannot grant extra actions.");
            Assert.That(PowerManager.Instance.IsBusy, Is.False);
            Assert.That(caster.GetComponent<BotPlayerController>().ExecutedSteps, Is.GreaterThan(1));
            Assert.That(blocker.GetComponent<BotPlayerController>().ExecutedSteps, Is.GreaterThan(1));
            Assert.That(counter.GetComponent<BotPlayerController>().ExecutedSteps, Is.GreaterThan(0));
        }
        finally
        {
            manager.OnPowerActivated -= onActivation;
            manager.OnStateChanged -= onResolution;
            PauseAll();
            foreach (var original in originals)
                original.Key.Kingdom = original.Value;
            foreach (Kingdom clone in clones)
                Object.Destroy(clone);
        }
    }

    [UnityTest]
    public IEnumerator RainUsesEachAffectedOwnerAndWaitsForHumanAndPausedNoCapacityBot()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return RainScenario(false);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RainRemovalUsesAffectedOwnersAndContinuesPastAnEmptyPausedBot()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return RainScenario(true);
        yield return new ExitPlayMode();
    }

    private IEnumerator RainScenario(bool remove)
    {
        yield return AwaitSetup();
        Start(new[] { typeof(Rain), typeof(InfiniteKnowledge), typeof(Witchcraft), typeof(Invisibility) },
            new[] { true, false, true, true });
        Player caster = Players[0], human = Players[1], full = Players[2], last = Players[3];
        foreach (Player player in Players)
            ClearBoard(player);
        if (remove)
        {
            foreach (Player player in Players.Where(player => player != full))
                BusaraPlaytestTools.AddResources(player, ResourceType.Earth, 2);
        }
        else
        {
            foreach (Slot slot in full.Board.Slots)
                BusaraPlaytestTools.AddResources(full, ResourceType.Earth, 1, slot, true);
        }
        Virtue surplus = ForgeManager.Instance.AllVirtues.First(virtue =>
            !caster.Kingdom.virtuesForWin.Any(goal => goal.virtues.type == virtue.type));
        caster.Virtues.AddRange(Enumerable.Repeat(surplus, caster.Kingdom.power.virtueCost));
        PowerManager.Instance.RefreshPlaytestState();
        AssertGameRunning();
        PowerManager powers = PowerManager.Instance;
        powers.ActivatePower(caster.Kingdom.power);
        for (int i = 0; i < caster.Kingdom.power.virtueCost; i++)
            Select(PowerOptionKind.PaymentAdd, option => option.Virtue == surplus);
        Select(PowerOptionKind.Continue);
        Assert.That(powers.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.RainOperation));
        Select(PowerOptionKind.RainOperation, option => option.Remove == remove && option.Count == 1);
        Select(PowerOptionKind.Use);
        Assert.That(caster.Virtues, Is.Empty);
        Assert.That(powers.DecisionContext.Kind,
            Is.EqualTo(remove ? PowerDecisionKind.RemoveResource : PowerDecisionKind.AddResource));
        Assert.That(powers.DecisionContext.Owner, Is.SameAs(caster));
        caster.GetComponent<BotPlayerController>().SetPaused(false);
        phase = remove ? "Rain caster bot removes its own resource" : "Rain caster bot chooses its resource and slot";
        yield return WaitForOwner(human);
        Assert.That(PowerRules.Resources(caster).Count, Is.EqualTo(1));
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(caster));
        int humanRevision = powers.ChoiceRevision;
        yield return new WaitForSecondsRealtime(1.3f);
        Assert.That(powers.ChoiceRevision, Is.EqualTo(humanRevision), "Other bots must not select a human's Rain choices.");
        Assert.That(human.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
        if (remove)
        {
            Assert.That(powers.Choices.All(choice => choice.Option.Resource.slot.board.player == human), Is.True);
            Select(PowerOptionKind.Resource);
        }
        else
        {
            Select(PowerOptionKind.ResourceType);
            Assert.That(powers.DecisionContext.Owner, Is.SameAs(human));
            Assert.That(powers.Choices.All(choice => choice.Option.Slot.board.player == human), Is.True);
            Select(PowerOptionKind.Slot);
        }
        Assert.That(powers.DecisionContext.Owner, Is.SameAs(full));
        Assert.That(powers.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.Notice));
        int fullRevision = powers.ChoiceRevision;
        yield return new WaitForSecondsRealtime(1.3f);
        Assert.That(powers.ChoiceRevision, Is.EqualTo(fullRevision), "A no-capacity notice still belongs to the affected paused bot.");
        Assert.That(full.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
        full.GetComponent<BotPlayerController>().SetPaused(false);
        phase = "Rain no-capacity bot continues and final affected bot completes";
        yield return WaitForOwner(last);
        Assert.That(powers.DecisionContext.Kind,
            Is.EqualTo(remove ? PowerDecisionKind.RemoveResource : PowerDecisionKind.AddResource));
        last.GetComponent<BotPlayerController>().SetPaused(false);
        float deadline = Time.realtimeSinceStartup + 10;
        while (powers.IsBusy && Time.realtimeSinceStartup < deadline)
        {
            AssertBotsHealthy();
            yield return null;
        }
        PauseAll();
        AssertGameRunning();
        Assert.That(powers.IsBusy, Is.False);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        Assert.That(PowerRules.Resources(human).Count, Is.EqualTo(1));
        Assert.That(PowerRules.Resources(full).Count, Is.EqualTo(remove ? 0 : full.Board.Slots.Count));
        Assert.That(PowerRules.Resources(last).Count, Is.EqualTo(1));
        Assert.That(full.GetComponent<BotPlayerController>().ExecutedSteps, Is.EqualTo(1));
        Assert.That(last.GetComponent<BotPlayerController>().ExecutedSteps, Is.EqualTo(remove ? 1 : 2));
        Assert.That(human.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
    }

    [UnityTest]
    public IEnumerator InformationPowerContinuesAutomaticallyButManualInspectionDoesNot()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return InformationScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator InformationScenario()
    {
        yield return AwaitSetup();
        Start(new[] { typeof(InfiniteKnowledge), typeof(Witchcraft) }, new[] { true, false });
        Player caster = Players[0], human = Players[1];
        foreach (Player player in Players)
        {
            ClearBoard(player);
            BusaraPlaytestTools.AddResources(player, ResourceType.Earth, 1);
        }
        Virtue surplus = ForgeManager.Instance.AllVirtues.First(virtue =>
            !caster.Kingdom.virtuesForWin.Any(goal => goal.virtues.type == virtue.type));
        caster.Virtues.AddRange(Enumerable.Repeat(surplus, caster.Kingdom.power.virtueCost));
        PowerManager powers = PowerManager.Instance;
        powers.RefreshPlaytestState();
        AssertGameRunning();
        powers.InspectPlayer(caster);
        BotPlayerController bot = caster.GetComponent<BotPlayerController>();
        bot.SetPaused(false);
        int revision = powers.ChoiceRevision;
        yield return new WaitForSecondsRealtime(1.3f);
        Assert.That(bot.ExecutedSteps, Is.Zero);
        Assert.That(powers.DecisionContext.Kind, Is.EqualTo(PowerDecisionKind.ManualInspection));
        Assert.That(powers.ChoiceRevision, Is.EqualTo(revision));
        powers.Choices.Single(choice => choice.Option.Kind == PowerOptionKind.Continue).OnSelected();
        Assert.That(HeuristicBot.Analyze(caster).Chosen.Kind, Is.EqualTo(BotActionKind.UsePower));
        bool informationNotice = false;
        var phases = new HashSet<PowerDecisionKind>();
        phase = "Bot knowledge payment, target, confirm and information Continue";
        float deadline = Time.realtimeSinceStartup + 15;
        while (TurnManager.Instance.ActivePlayer == caster && Time.realtimeSinceStartup < deadline && !bot.Failed)
        {
            AssertGameRunning();
            if (powers.IsBusy)
            {
                phases.Add(powers.DecisionContext.Kind);
                if (powers.DecisionContext.Kind == PowerDecisionKind.Notice)
                {
                    informationNotice = true;
                    Assert.That(powers.DecisionContext.Owner, Is.SameAs(caster));
                    Assert.That(HeuristicBot.Analyze(caster).Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Continue));
                }
            }
            yield return null;
        }
        bot.SetPaused(true);
        AssertGameRunning();
        Assert.That(bot.Failed, Is.False, bot.Status);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        Assert.That(informationNotice, Is.True);
        Assert.That(phases, Does.Contain(PowerDecisionKind.Payment));
        Assert.That(phases, Does.Contain(PowerDecisionKind.Target));
        Assert.That(phases, Does.Contain(PowerDecisionKind.Confirm));
        Assert.That(caster.knownKingdoms.Contains(human.Kingdom), Is.True);
        Assert.That(human.kingdomRevealed, Is.False, "Privately learning a kingdom must not reveal it to everyone.");
        Assert.That(caster.Virtues, Is.Empty);
        yield return new WaitForSecondsRealtime(1.2f);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        Assert.That(human.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
    }

    [UnityTest]
    public IEnumerator WitchcraftBotPaysThenStrictlyImprovesAndFinishesRearrangement()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return WitchcraftScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator WitchcraftScenario()
    {
        yield return AwaitSetup();
        Start(new[] { typeof(Witchcraft), typeof(InfiniteKnowledge) }, new[] { true, false });
        Player caster = Players[0], human = Players[1];
        Kingdom original = caster.Kingdom;
        Virtue goal = Virtue(VirtueType.Art), surplus = Virtue(VirtueType.Nature);
        Kingdom clone = CloneGoals(caster, goal, 3);
        BotPlayerController bot = caster.GetComponent<BotPlayerController>();
        try
        {
            foreach (Player player in Players)
                ClearBoard(player);
            BusaraPlaytestTools.AddResources(human, ResourceType.Earth, 1);
            Slot[] slots = caster.Board.Slots.OrderBy(slot => slot.Index).ToArray();
            for (int i = 0; i < slots.Length; i++)
                BusaraPlaytestTools.AddResources(caster,
                    i < slots.Length / 2 ? goal.componentOne : goal.componentTwo, 1, slots[i], true);
            caster.Virtues.AddRange(Enumerable.Repeat(surplus, caster.Kingdom.power.virtueCost));
            PowerManager powers = PowerManager.Instance;
            powers.RefreshPlaytestState();
            AssertGameRunning();
            Assert.That(BotPowerPlanner.Plan(caster).Score, Is.GreaterThan(0),
                "The monochrome halves must offer improvements worth the catalog activation cost.");
            float previous = BotPowerPlanner.BoardValue(caster, caster);
            int improvingMoves = 0;
            bool finishOffered = false;
            powers.ActivatePower(caster.Kingdom.power);
            bot.SetPaused(false);
            phase = "Witchcraft strict board improvements followed by Finish";
            float deadline = Time.realtimeSinceStartup + 30;
            while (TurnManager.Instance.ActivePlayer == caster && Time.realtimeSinceStartup < deadline && !bot.Failed)
            {
                AssertGameRunning();
                float current = BotPowerPlanner.BoardValue(caster, caster);
                Assert.That(current, Is.GreaterThanOrEqualTo(previous), "The bot must never undo a better arrangement.");
                if (current > previous)
                    improvingMoves++;
                previous = current;
                if (powers.IsBusy && powers.DecisionContext.Kind == PowerDecisionKind.RearrangeResource)
                    finishOffered |= HeuristicBot.Analyze(caster).Chosen.Choice.Option.Kind == PowerOptionKind.Finish;
                yield return null;
            }
            bot.SetPaused(true);
            AssertGameRunning();
            Assert.That(bot.Failed, Is.False, bot.Status);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
            Assert.That(improvingMoves, Is.GreaterThan(0));
            Assert.That(finishOffered, Is.True);
            Assert.That(caster.Virtues, Is.Empty);
            Assert.That(PowerRules.Resources(caster).Count, Is.EqualTo(slots.Length));
            Assert.That(powers.IsBusy, Is.False);
            Assert.That(bot.LastDecision.Chosen.Choice.Option.Kind, Is.EqualTo(PowerOptionKind.Finish));
        }
        finally
        {
            bot.SetPaused(true);
            caster.Kingdom = original;
            Object.Destroy(clone);
        }
    }

    private IEnumerator AwaitSetup()
    {
        Application.runInBackground = true;
        phase = "Awaiting GameScene initialization";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        Assert.That(Object.FindFirstObjectByType<PlayerSetupUI>(), Is.Not.Null);
    }

    private static List<Player> Players => PlayerManager.Instance.Players;

    private static void Start(Type[] powers, bool[] bots)
    {
        KingdomCatalog catalog = PlayerManager.Instance.kingdomCatalog;
        BusaraPlaytestTools.StartGame(powers.Select((type, index) => new PlayerSetupEntry(
            "Power scenario " + index, catalog.kingdoms.Single(kingdom => kingdom.power.GetType() == type), bots[index])).ToArray());
        PauseAll();
        BusaraPlaytestTools.FinishResourceSetup();
        Assert.That(PowerManager.Instance.IsBusy, Is.False);
        AssertGameRunning();
    }

    private static void PauseAll()
    {
        foreach (Player player in Players)
            player.GetComponent<BotPlayerController>().SetPaused(true);
    }

    private static void AssertBotsHealthy()
    {
        AssertGameRunning();
        foreach (Player player in Players.Where(player => player.IsBotControlled))
        {
            BotPlayerController bot = player.GetComponent<BotPlayerController>();
            Assert.That(bot.Failed, Is.False, player.Name + ": " + bot.Status);
        }
    }

    private static void AssertGameRunning()
    {
        Assert.That(TurnManager.Instance.enabled, Is.True, "The fixture must not enter the empty-board stalemate path.");
        Assert.That(GameManager.Instance.WinScreen.activeSelf, Is.False, "Fixture inventories must not trigger a win.");
        Assert.That(GameManager.Instance.DrawScreen.activeSelf, Is.False, "Fixture boards must not trigger a draw.");
        foreach (Player player in Players)
        {
            Assert.That(player.Kingdom.virtuesForWin, Is.Not.Empty);
            Assert.That(player.Kingdom.virtuesForWin.All(goal =>
                player.Virtues.Count(virtue => virtue.type == goal.virtues.type) >= goal.NumberofVirtues),
                Is.False, player.Name + " must remain below its victory threshold.");
        }
    }

    private IEnumerator WaitForOwner(Player owner)
    {
        float deadline = Time.realtimeSinceStartup + 10;
        while (PowerManager.Instance.IsBusy && PowerManager.Instance.DecisionContext.Owner != owner &&
            Time.realtimeSinceStartup < deadline)
        {
            AssertBotsHealthy();
            yield return null;
        }
        Assert.That(PowerManager.Instance.IsBusy, Is.True, phase);
        Assert.That(PowerManager.Instance.DecisionContext.Owner, Is.SameAs(owner), phase);
    }

    private static void Select(PowerOptionKind kind, Func<PowerOption, bool> predicate = null)
    {
        PowerManager manager = PowerManager.Instance;
        Assert.That(manager.DecisionContext, Is.Not.Null);
        PowerChoice choice = manager.Choices.FirstOrDefault(candidate => candidate.Enabled &&
            candidate.Option != null && candidate.Option.Kind == kind && (predicate == null || predicate(candidate.Option)));
        Assert.That(choice, Is.Not.Null, "Missing typed " + kind + " during " + manager.DecisionContext.Kind);
        Assert.That(manager.TrySelectChoice(manager.DecisionContext.Owner, manager.ChoiceRevision, choice), Is.True);
    }

    private static Virtue Virtue(VirtueType type) => ForgeManager.Instance.AllVirtues.Single(virtue => virtue.type == type);

    private static void ClearBoard(Player player)
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            BusaraPlaytestTools.RemoveResources(player, type, 100);
    }

    private static Kingdom CloneGoals(Player player, Virtue virtue, int count)
    {
        Kingdom clone = Object.Instantiate(player.Kingdom);
        // The real kingdom card displays three goal rows; Nature remains the payment surplus.
        Virtue[] goals = new[] { virtue }.Concat(ForgeManager.Instance.AllVirtues
            .Where(item => item.type != virtue.type && item.type != VirtueType.Nature).Take(2)).ToArray();
        Assert.That(goals.Length, Is.EqualTo(3));
        clone.virtuesForWin = goals.Select(item =>
            new Kingdom.VirtuesForCost { virtues = item, NumberofVirtues = count }).ToArray();
        player.Kingdom = clone;
        return clone;
    }

    private static void PlacePair(Player player, Virtue virtue)
    {
        Slot first = player.Board.Slots.First(slot =>
            BoardManager.GetAdjacentSlots(slot).Any(next => next.board == player.Board));
        Slot next = BoardManager.GetAdjacentSlots(first).First(slot => slot.board == player.Board);
        BusaraPlaytestTools.AddResources(player, virtue.componentOne, 1, first);
        BusaraPlaytestTools.AddResources(player, virtue.componentTwo, 1, next);
    }

    private static void PrepareScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Do not discard scene edits for bot power tests.");
        SessionState.SetString(BackupKey, JsonUtility.ToJson(new SceneBackup
        {
            Scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneRecord
            {
                Path = scene.path, Loaded = scene.isLoaded, Active = scene.isActive
            }).ToArray()
        }));
        SessionState.SetBool(BackgroundKey, Application.runInBackground);
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            Debug.Log("Bot power scene failure (" + phase + "): " + TestContext.CurrentContext.Result.StackTrace);
        if (Application.isPlaying)
            yield return new ExitPlayMode();
        string json = SessionState.GetString(BackupKey, "");
        SessionState.EraseString(BackupKey);
        if (string.IsNullOrEmpty(json))
            yield break;
        Application.runInBackground = SessionState.GetBool(BackgroundKey, Application.runInBackground);
        SessionState.EraseBool(BackgroundKey);
        SceneSetup[] scenes = JsonUtility.FromJson<SceneBackup>(json).Scenes.Select(scene => new SceneSetup
        {
            path = scene.Path, isLoaded = scene.Loaded, isActive = scene.Active
        }).ToArray();
        if (scenes.Length > 0 && scenes.All(scene => !string.IsNullOrEmpty(scene.path)))
            EditorSceneManager.RestoreSceneManagerSetup(scenes);
        else
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [Serializable]
    private class SceneBackup { public SceneRecord[] Scenes; }

    [Serializable]
    private class SceneRecord
    {
        public string Path;
        public bool Loaded;
        public bool Active;
    }
}
