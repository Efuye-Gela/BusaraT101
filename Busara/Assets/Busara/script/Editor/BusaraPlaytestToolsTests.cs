using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class BusaraPlaytestToolsTests
{
    private const string BackupKey = "Busara.PlaytestToolsTests.SceneBackup";
    private const string BackgroundKey = "Busara.PlaytestToolsTests.RunInBackground";
    private string phase;

    [Test]
    public void BotWindowRebindsDestroyedPlayerAndClearsStaleAnalysis()
    {
        var oldObject = new GameObject("Previous match player") { hideFlags = HideFlags.HideAndDontSave };
        var liveObject = new GameObject("Current match player") { hideFlags = HideFlags.HideAndDontSave };
        var window = ScriptableObject.CreateInstance<BusaraBotWindow>();
        try
        {
            Player previous = oldObject.AddComponent<Player>();
            Player current = liveObject.AddComponent<Player>();
            var analysis = (BotDecision)Activator.CreateInstance(typeof(BotDecision),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { previous, Array.Empty<BotCandidate>(), "Previous match" }, null);
            window.ShowAnalysis(analysis);
            var field = typeof(BusaraBotWindow).GetField("decision",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            window.BindLivePlayer(previous);
            Assert.That(field.GetValue(window), Is.SameAs(analysis), "A repaint must retain a live player's analysis.");
            Object.DestroyImmediate(oldObject);
            window.BindLivePlayer(current);
            Assert.That(new SerializedObject(window).FindProperty("player").objectReferenceValue, Is.SameAs(current));
            Assert.That(field.GetValue(window), Is.Null, "A new match must not offer the previous player's decision.");
        }
        finally
        {
            Object.DestroyImmediate(window);
            if (oldObject != null)
                Object.DestroyImmediate(oldObject);
            Object.DestroyImmediate(liveObject);
        }
    }

    [UnityTest]
    public IEnumerator WinterBotsDiscardOnceAroundHumanAndResumeOriginalDrawer()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return WinterBotScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator WinterBotScenario()
    {
        Application.runInBackground = true;
        phase = "Preparing mixed Hard Winter participants";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        Kingdom[] kingdoms = PlayerManager.Instance.kingdomCatalog.kingdoms
            .Where(kingdom => kingdom.power.timing == PowerTiming.OwnTurn).Take(4).ToArray();
        BusaraPlaytestTools.StartGame(kingdoms.Select((kingdom, i) =>
            new PlayerSetupEntry("Winter " + i, kingdom, i != 1)).ToArray());
        Player[] players = PlayerManager.Instance.Players.ToArray();
        foreach (Player player in players)
            player.GetComponent<BotPlayerController>().SetPaused(true);
        BusaraPlaytestTools.FinishResourceSetup();
        Player first = players[0], human = players[1], empty = players[2], last = players[3];
        Virtue needed = first.Kingdom.virtuesForWin.First().virtues;
        Virtue surplus = ForgeManager.Instance.AllVirtues.First(virtue =>
            !first.Kingdom.virtuesForWin.Any(goal => goal.virtues.type == virtue.type));
        first.Virtues.AddRange(new[] { needed, surplus });
        human.Virtues.Add(needed);
        last.Virtues.Add(surplus);
        first.virtuesHidden = true;
        last.virtuesHidden = true;
        HardWinterDisaster winter = DeckManager.Instance.Cards.OfType<DisasterCard>()
            .Select(card => card.effect).OfType<HardWinterDisaster>().First();
        int completed = 0;
        Action ended = () => completed++;
        DisasterManager.OnDisasterEnd += ended;
        try
        {
            DisasterManager.Instance.TriggerDisaster(winter);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(first));
            BotDecision stale = HeuristicBot.Analyze(first);
            Assert.That(stale.Chosen.DiscardType, Is.EqualTo(surplus.type));
            first.Virtues.Add(surplus);
            Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(stale));
            first.Virtues.Remove(surplus);
            stale = HeuristicBot.Analyze(first);
            PowerManager.Instance.InspectPlayer(first);
            first.GetComponent<BotPlayerController>().SetPaused(false);
            empty.GetComponent<BotPlayerController>().SetPaused(false);
            last.GetComponent<BotPlayerController>().SetPaused(false);
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(first.Virtues.Count, Is.EqualTo(2), "A modal must block even a ready bot discard.");
            Assert.That(completed, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(stale));
            PowerManager.Instance.Choices.Single(choice => choice.Label == "Continue").OnSelected();
            phase = "First bot discards but leaves the human's choice untouched";
            float deadline = Time.realtimeSinceStartup + 5;
            while (TurnManager.Instance.ActivePlayer == first && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
            CollectionAssert.AreEqual(new[] { needed }, first.Virtues);
            Assert.That(first.virtuesHidden, Is.True);
            Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(stale));
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(human.Virtues.Count, Is.EqualTo(1));
            Assert.That(last.Virtues.Count, Is.EqualTo(1));
            Assert.That(completed, Is.Zero);
            Assert.That(winter.TryDiscard(human, needed), Is.True);
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(last), "Empty inventory is skipped.");
            phase = "Final bot completes the whole disaster exactly once";
            deadline = Time.realtimeSinceStartup + 5;
            while (HardWinterDisaster.Active != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(HardWinterDisaster.Active, Is.Null);
            Assert.That(TurnManager.Instance.isSpecialCardDrawn, Is.False);
            Assert.That(last.Virtues, Is.Empty);
            Assert.That(last.virtuesHidden, Is.True);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human), "Resume after the original drawer, not the final bot.");
            Assert.That(first.GetComponent<BotPlayerController>().ExecutedSteps, Is.EqualTo(1));
            Assert.That(last.GetComponent<BotPlayerController>().ExecutedSteps, Is.EqualTo(1));
            Assert.That(empty.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
            BotDecision lastDecision = last.GetComponent<BotPlayerController>().LastDecision;
            Assert.That(lastDecision.Chosen.Kind, Is.EqualTo(BotActionKind.DiscardVirtue));
            Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(lastDecision));
            yield return new WaitForSecondsRealtime(1.3f);
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        }
        finally
        {
            DisasterManager.OnDisasterEnd -= ended;
        }
    }

    [UnityTest]
    public IEnumerator AssignedBotsActAutomaticallyAndLeaveHumanTurnAlone()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return AssignedBotScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator AssignedBotScenario()
    {
        Application.runInBackground = true;
        phase = "Configuring two assigned bots and a human";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        Kingdom[] kingdoms = PlayerManager.Instance.kingdomCatalog.kingdoms
            .Where(kingdom => kingdom.power.timing == PowerTiming.OwnTurn).Take(3).ToArray();
        BusaraPlaytestTools.StartGame(new[]
        {
            new PlayerSetupEntry("Auto bot 1", kingdoms[0], true),
            new PlayerSetupEntry("Auto bot 2", kingdoms[1], true),
            new PlayerSetupEntry("Human", kingdoms[2])
        });
        Player first = PlayerManager.Instance.Players[0];
        Player second = PlayerManager.Instance.Players[1];
        Player human = PlayerManager.Instance.Players[2];
        BotPlayerController firstBot = first.GetComponent<BotPlayerController>();
        BotPlayerController secondBot = second.GetComponent<BotPlayerController>();
        Assert.That(firstBot, Is.Not.Null);
        Assert.That(secondBot, Is.Not.Null);
        Assert.That(first.IsBotControlled && second.IsBotControlled, Is.True);
        Assert.That(human.IsBotControlled, Is.False);
        firstBot.SetPaused(true);
        secondBot.SetPaused(true);
        BusaraPlaytestTools.FinishResourceSetup();
        foreach (Player player in new[] { first, second })
        {
            // Isolate ordinary actions from hidden-only Abundance, now also evaluated by bots.
            player.kingdomRevealed = true;
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                BusaraPlaytestTools.RemoveResources(player, type, 100);
            BusaraPlaytestTools.AddResources(player, ResourceType.Earth, 1);
        }
        ResourceCard earth = DeckManager.Instance.Cards.OfType<ResourceCard>().First(card => card.Resource == ResourceType.Earth);
        DeckManager.Instance.Cards = Enumerable.Repeat<Card>(earth, 4).ToList();
        DeckManager.Instance.CardCount = 4;
        PowerManager.Instance.InspectPlayer(first);
        firstBot.SetPaused(false);
        secondBot.SetPaused(false);
        phase = "Waiting at a modal without bypassing it";
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(firstBot.ExecutedSteps, Is.Zero);
        Assert.That(secondBot.ExecutedSteps, Is.Zero);
        PowerManager.Instance.Choices.Single(choice => choice.Label == "Continue").OnSelected();
        phase = "Bots analyze, draw, place and complete their own turns without an Editor window";
        float deadline = Time.realtimeSinceStartup + 15;
        while (TurnManager.Instance.ActivePlayer != human && !firstBot.Failed && !secondBot.Failed &&
            Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(firstBot.Failed, Is.False, firstBot.Status);
        Assert.That(secondBot.Failed, Is.False, secondBot.Status);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        Assert.That(firstBot.ExecutedSteps, Is.EqualTo(2));
        Assert.That(secondBot.ExecutedSteps, Is.EqualTo(2));
        Assert.That(firstBot.LastDecision.Chosen.Kind, Is.EqualTo(BotActionKind.Place));
        Assert.That(firstBot.History.Count, Is.EqualTo(2));
        Assert.That(secondBot.History.Count, Is.EqualTo(2));
        Assert.That(first.Board.GetOccupiedSlots().Count, Is.EqualTo(2));
        Assert.That(second.Board.GetOccupiedSlots().Count, Is.EqualTo(2));
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(human));
        Assert.That(human.GetComponent<BotPlayerController>().ExecutedSteps, Is.Zero);
        Assert.That(firstBot.ExecutedSteps, Is.EqualTo(2), "Do not end the turn a second time from the scheduler.");
        Assert.That(secondBot.ExecutedSteps, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator FastTestStartEntersPlayModeAndCompletesSetup()
    {
        PrepareScene();
        yield return new FastStartPlayMode();
        yield return AssertFastStartCompleted();
        yield return new ExitPlayMode();
    }

    private sealed class FastStartPlayMode : IEditModeTestYieldInstruction
    {
        public bool ExpectDomainReload => true;
        public bool ExpectedPlaymodeState { get; private set; }

        public IEnumerator Perform()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("This scenario must start in Edit Mode.");
            yield return null;
            ExpectedPlaymodeState = true;
            EditorApplication.UnlockReloadAssemblies();
            QueueFastStart();
            while (!EditorApplication.isPlaying)
                yield return null;
        }
    }

    [UnityTest]
    public IEnumerator FastTestStartWorksWhileAlreadyAwaitingSetupInPlayMode()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return StartFastFromPlayMode();
        yield return new ExitPlayMode();
    }

    private IEnumerator StartFastFromPlayMode()
    {
        Application.runInBackground = true;
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        QueueFastStart();
        yield return AssertFastStartCompleted();
    }

    private static void QueueFastStart()
    {
        Assert.That(BusaraFastTestStart.IsPending, Is.False, "Do not replace another pending startup.");
        PlayerManager manager = Object.FindFirstObjectByType<PlayerManager>();
        BusaraFastTestStart.Begin(manager.kingdomCatalog.kingdoms
            .Where(kingdom => kingdom.power.timing == PowerTiming.OwnTurn).Take(2)
            .Select((kingdom, index) => new PlayerSetupEntry("Fast player " + (index + 1), kingdom, index == 1)).ToArray());
    }

    private IEnumerator AssertFastStartCompleted()
    {
        Application.runInBackground = true;
        phase = "Waiting for the persisted fast-start request";
        float deadline = Time.realtimeSinceStartup + 20;
        while (BusaraFastTestStart.IsPending && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(BusaraFastTestStart.IsPending, Is.False, BusaraFastTestStart.Status);
        Assert.That(BusaraFastTestStart.Failed, Is.False, BusaraFastTestStart.Status);
        Assert.That(BusaraFastTestStart.CompletedRequestId, Is.Not.Empty);
        PlayerManager manager = PlayerManager.Instance;
        Assert.That(manager.Players.Count, Is.EqualTo(2));
        Assert.That(manager.Players[0].IsBotControlled, Is.False);
        Assert.That(manager.Players[1].IsBotControlled, Is.True);
        Assert.That(manager.Players[1].GetComponent<BotPlayerController>(), Is.Not.Null);
        CollectionAssert.AreEqual(new[] { "Fast player 1", "Fast player 2" }, manager.Players.Select(player => player.Name));
        Assert.That(manager.Players.Select(player => player.Kingdom).Distinct().Count(), Is.EqualTo(2));
        foreach (Player player in manager.Players)
        {
            Assert.That(player.hasFinishedSettingUp, Is.True);
            Assert.That(player.setUpCard, Is.Null);
            Assert.That(player.Board.GetOccupiedSlots(), Is.Not.Empty);
            foreach (Slot slot in player.Board.GetOccupiedSlots())
                Assert.That(BoardManager.GetAdjacentSlots(slot).Any(neighbor =>
                    player.Board.Slots.Contains(neighbor) && neighbor.isOccupied), Is.False);
        }
        Assert.That(ActionManager.Instance.CurrentState, Is.EqualTo(ActionManager.ActionState.None));
        Assert.Throws<InvalidOperationException>(() => QueueFastStart());
        Assert.That(BusaraFastTestStart.IsPending, Is.False);
    }

    [UnityTest]
    public IEnumerator BotDecisionsUseNormalActionsAndRejectStalePlans()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return BotScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator BotScenario()
    {
        Application.runInBackground = true;
        phase = "Starting bot action scenario";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        var kingdoms = PlayerManager.Instance.kingdomCatalog.kingdoms
            .Where(kingdom => kingdom.power.timing == PowerTiming.OwnTurn).Take(2).ToArray();
        BusaraPlaytestTools.StartGame(new[]
        {
            new PlayerSetupEntry("Bot player", kingdoms[0]), new PlayerSetupEntry("Human player", kingdoms[1])
        });
        BusaraPlaytestTools.FinishResourceSetup();
        Player first = PlayerManager.Instance.Players[0];
        Player second = PlayerManager.Instance.Players[1];
        first.kingdomRevealed = true;
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            BusaraPlaytestTools.RemoveResources(first, type, 100);
        BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 1);
        DeckManager deck = DeckManager.Instance;
        ResourceCard resourceCard = deck.Cards.OfType<ResourceCard>().First();
        BusaraPlaytestTools.ForceNextCard(resourceCard);
        phase = "Scoring and executing an unknown draw";
        BotDecision drawing = HeuristicBot.Analyze(first);
        Assert.That(drawing.Chosen.Kind, Is.EqualTo(BotActionKind.Draw));
        var before = first.Board.GetOccupiedSlots().ToArray();
        deck.Cards.Reverse();
        BotDecision repeat = HeuristicBot.Analyze(first);
        deck.Cards.Reverse();
        CollectionAssert.AreEqual(drawing.Candidates.Select(path => path.Score), repeat.Candidates.Select(path => path.Score));
        CollectionAssert.AreEqual(before, first.Board.GetOccupiedSlots());
        if (!EditorWindow.HasOpenInstances<BusaraBotWindow>())
        {
            var window = EditorWindow.GetWindow<BusaraBotWindow>();
            try
            {
                window.ShowAnalysis(drawing);
                window.Show();
                yield return null;
                window.Repaint();
                yield return null;
            }
            finally
            {
                window.Close();
            }
        }
        PowerManager.Instance.InspectPlayer(first);
        Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(drawing));
        PowerManager.Instance.Choices.Single(choice => choice.Label == "Continue").OnSelected();
        HeuristicBot.Execute(drawing);
        Assert.That(first.hasDrawnResource, Is.True);
        BotDecision placing = HeuristicBot.Analyze(first);
        Assert.That(placing.Chosen.Kind, Is.EqualTo(BotActionKind.Place));
        Slot placed = first.Board.GetSlotByIndex(placing.Chosen.To);
        HeuristicBot.Execute(placing);
        Assert.That(placed.resource.resourceType, Is.EqualTo(resourceCard.Resource));
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second));
        yield return null;

        phase = "Building a deterministic move and forge fixture";
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            BusaraPlaytestTools.RemoveResources(first, type, 100);
        Virtue goal = first.Kingdom.virtuesForWin.First().virtues;
        Slot source = first.Board.Slots.OrderBy(slot => slot.Index).First();
        Slot middle = BoardManager.GetAdjacentSlots(source).Where(slot => first.Board.Slots.Contains(slot))
            .OrderBy(slot => slot.Index).First();
        Slot far = BoardManager.GetAdjacentSlots(middle).Where(slot => first.Board.Slots.Contains(slot) &&
            slot != source && !BoardManager.GetAdjacentSlots(source).Contains(slot)).OrderBy(slot => slot.Index).First();
        BusaraPlaytestTools.AddResources(first, goal.componentOne, 1, source);
        BusaraPlaytestTools.AddResources(first, goal.componentTwo, 1, far);
        // Deliberately unavailable deck isolates movement from the unknown-draw heuristic.
        Card[] deckBefore = deck.Cards.ToArray();
        deck.Cards.Clear();
        BusaraPlaytestTools.EndTurn();
        BotDecision movement = HeuristicBot.Analyze(first);
        Assert.That(movement.Chosen.Kind, Is.EqualTo(BotActionKind.Move));
        BusaraPlaytestTools.AddVirtues(first, goal, 1);
        Assert.Throws<InvalidOperationException>(() => HeuristicBot.Execute(movement));
        BusaraPlaytestTools.RemoveVirtues(first, goal.type, 1);
        movement = HeuristicBot.Analyze(first);
        Resource moving = first.Board.GetSlotByIndex(movement.Chosen.From).resource;
        Slot destination = first.Board.GetSlotByIndex(movement.Chosen.To);
        HeuristicBot.Execute(movement);
        Assert.That(destination.resource, Is.SameAs(moving));
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second));
        BusaraPlaytestTools.EndTurn();
        phase = "Forging the scored pair through the normal action";
        BotDecision forging = HeuristicBot.Analyze(first);
        Assert.That(forging.Chosen.Kind, Is.EqualTo(BotActionKind.Forge));
        HeuristicBot.Execute(forging);
        Assert.That(first.Virtues.Any(virtue => virtue.type == goal.type), Is.True);
        Assert.That(first.Board.GetOccupiedSlots(), Is.Empty);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(second));
        deck.Cards.AddRange(deckBefore);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RightSidebarPowerButtonActivatesCurrentPlayersPower()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return PowerButtonScenario();
        yield return new ExitPlayMode();
    }

    private IEnumerator PowerButtonScenario()
    {
        Application.runInBackground = true;
        EditorWindow gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        gameView.Focus();
        phase = "Starting a game for the sidebar power button";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        Kingdom[] catalog = PlayerManager.Instance.kingdomCatalog.kingdoms.ToArray();
        BusaraPlaytestTools.StartGame(new[]
        {
            new PlayerSetupEntry("Magic player", catalog.First(kingdom => kingdom.power is Magic)),
            new PlayerSetupEntry("Other player", catalog.First(kingdom => kingdom.power is Abundance))
        });
        BusaraPlaytestTools.FinishResourceSetup();
        Player player = TurnManager.Instance.ActivePlayer;
        Virtue virtue = ForgeManager.Instance.AllVirtues.First();
        int cost = player.Kingdom.power.virtueCost;
        BusaraPlaytestTools.AddVirtues(player, virtue, cost);
        yield return null;
        Button button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(item => item.name == "Use Power");
        Assert.That(button.transform.parent.name, Is.EqualTo("Action Buttons"));
        Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("Use Power"));
        Assert.That(button.onClick.GetPersistentTarget(0), Is.TypeOf<PowerActionMove>());
        Assert.That(button.onClick.GetPersistentMethodName(0), Is.EqualTo("OnTapPower"));
        var panel = (RectTransform)button.transform.parent;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, button.transform);
        Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(panel.rect.yMin - 0.5f));
        Assert.That(bounds.max.y, Is.LessThanOrEqualTo(panel.rect.yMax + 0.5f));

        phase = "Clicking Use Power through the actual sidebar";
        yield return HardWinterDisasterSceneTests.ClickThroughEventSystem(button);
        PowerManager powers = PowerManager.Instance;
        Assert.That(powers.IsBusy, Is.True);
        StringAssert.Contains("Magic player", powers.ChoiceTitle);
        StringAssert.Contains("Choose payment", powers.ChoiceTitle);
        powers.Choices.Single(choice => choice.Label == "Cancel / pass").OnSelected();
        Assert.That(player.Virtues, Has.Count.EqualTo(cost));
        Assert.That(player.kingdomRevealed, Is.False);
        yield return null;

        phase = "Paying and activating the current player's power";
        yield return HardWinterDisasterSceneTests.ClickThroughEventSystem(button);
        for (int i = 0; i < cost; i++)
            powers.Choices.Single(choice => choice.Label.StartsWith("+ ") && choice.Enabled).OnSelected();
        powers.Choices.Single(choice => choice.Label == "Continue").OnSelected();
        powers.Choices.Single(choice => choice.Label == "Use power").OnSelected();
        Assert.That(player.Virtues, Is.Empty);
        Assert.That(player.kingdomRevealed, Is.True);
        Assert.That(TurnManager.Instance.ActivePlayer, Is.SameAs(player));
        StringAssert.Contains("2 action(s)", powers.ChoiceTitle);
        powers.Choices.Single(choice => choice.Label == "Continue").OnSelected();
    }

    [UnityTest]
    public IEnumerator RealGameSceneFixturesAndQueuedDisasterUseNormalLifecycle()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        // Captured scenario locals must be allocated after the Play Mode domain reload.
        yield return PlayScenario();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EditorSetupStartsThreePlayerGame()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return SetupOnlyScenario(3);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EditorSetupStartsFourPlayerGame()
    {
        PrepareScene();
        yield return new EnterPlayMode();
        yield return SetupOnlyScenario(4);
        yield return new ExitPlayMode();
    }

    private static void PrepareScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False, "Do not discard scene edits for this test.");
        SessionState.SetString(BackupKey, JsonUtility.ToJson(new SceneBackup
        {
            Scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SceneRecord
            {
                Path = scene.path, Loaded = scene.isLoaded, Active = scene.isActive
            }).ToArray()
        }));
        SessionState.SetBool(BackgroundKey, Application.runInBackground);
        Assert.That(BusaraPlaytestTools.EditBlockReason(), Is.Not.Empty);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.EndTurn());
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.StartGame(null));
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
    }

    private IEnumerator SetupOnlyScenario(int count)
    {
        Application.runInBackground = true;
        phase = "Configuring " + count + " players from the Editor backend";
        for (int frame = 0; frame < 120 && Object.FindFirstObjectByType<PlayerSetupUI>() == null; frame++)
            yield return null;
        PlayerManager manager = PlayerManager.Instance;
        PlayerSetupEntry[] entries = Enumerable.Range(0, count).Select(index =>
            new PlayerSetupEntry("Playtest player " + (index + 1), manager.kingdomCatalog.kingdoms[index])).ToArray();
        Kingdom chosen = entries[1].Kingdom;
        entries[1].Kingdom = entries[0].Kingdom;
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.StartGame(entries));
        Assert.That(manager.IsSetupComplete, Is.False);
        Assert.That(TurnManager.Instance.TurnsStarted, Is.False);
        entries[1].Kingdom = chosen;
        BusaraPlaytestTools.StartGame(entries);
        yield return null;
        Assert.That(manager.Players, Has.Count.EqualTo(count));
        Assert.That(manager.Players.Select(player => player.Name), Is.EqualTo(entries.Select(entry => entry.Name)));
        Assert.That(manager.Players.Select(player => player.Kingdom), Is.EqualTo(entries.Select(entry => entry.Kingdom)));
        Assert.That(Object.FindFirstObjectByType<PlayerSetupUI>().GetComponentsInChildren<Canvas>(), Is.Empty);
        Assert.That(TurnManager.Instance.TurnsStarted, Is.True);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.StartGame(entries));
        var required = manager.Players.ToDictionary(player => player, player => player.setUpCard.collectionResources.ToArray());
        BusaraPlaytestTools.FinishResourceSetup();
        foreach (Player player in manager.Players)
            AssertLegalSetup(player, required[player]);
    }

    private IEnumerator PlayScenario()
    {
        Application.runInBackground = true;
        EditorWindow gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.Show();
        gameView.Focus();
        phase = "Waiting for runtime player setup";
        for (int frame = 0; frame < 120; frame++)
        {
            PlayerSetupUI candidate = Object.FindFirstObjectByType<PlayerSetupUI>();
            if (candidate != null && candidate.GetComponentsInChildren<TMP_InputField>().Length == 2 &&
                candidate.GetComponentsInChildren<TMP_Dropdown>().Length == 2)
                break;
            yield return null;
        }
        PlayerSetupUI setup = Object.FindFirstObjectByType<PlayerSetupUI>();
        Assert.That(setup, Is.Not.Null);
        PlayerManager manager = PlayerManager.Instance;
        TMP_InputField[] names = setup.GetComponentsInChildren<TMP_InputField>();
        TMP_Dropdown[] dropdowns = setup.GetComponentsInChildren<TMP_Dropdown>();
        Assert.That(names, Has.Length.EqualTo(2));
        Assert.That(dropdowns, Has.Length.EqualTo(2));
        // Own-turn powers keep this disaster scenario independent of optional reaction prompts.
        Kingdom[] kingdoms = manager.kingdomCatalog.kingdoms
            .Where(kingdom => kingdom != null && kingdom.power != null && kingdom.power.timing == PowerTiming.OwnTurn)
            .Take(2).ToArray();
        Assert.That(kingdoms, Has.Length.EqualTo(2));
        BusaraPlaytestTools.StartGame(kingdoms.Select((kingdom, index) =>
            new PlayerSetupEntry("Playtest player " + (index + 1), kingdom)).ToArray());
        yield return null;
        Assert.That(Application.isPlaying, Is.True);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("GameScene"));
        Assert.That(manager.IsSetupComplete, Is.True);
        Assert.That(manager.Players, Has.Count.EqualTo(2));
        Player first = manager.Players[0];
        Player second = manager.Players[1];
        TurnManager turns = TurnManager.Instance;
        Assert.That(turns.ActivePlayer, Is.SameAs(first));

        phase = "Completing resource setup";
        Assert.That(BusaraPlaytestTools.EditBlockReason(true), Is.Empty);
        Assert.That(BusaraPlaytestTools.EditBlockReason(), Is.Not.Empty);
        var required = manager.Players.ToDictionary(player => player, player => player.setUpCard.collectionResources.ToArray());
        Slot originalSlot = first.Board.Slots[0];
        Assert.That(BusaraPlaytestTools.AddResources(first, required[first][0], 1, originalSlot), Is.EqualTo(1));
        Resource preserved = originalSlot.resource;
        Slot adjacent = BoardManager.GetAdjacentSlots(originalSlot).First(slot => first.Board.Slots.Contains(slot));
        BusaraPlaytestTools.AddResources(first, required[first][1], 1, adjacent);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.FinishResourceSetup());
        Assert.That(manager.Players.All(player => !player.hasFinishedSettingUp), Is.True);
        Assert.That(second.Board.GetOccupiedSlots(), Is.Empty, "Rejected setup must not partially populate another board.");
        BusaraPlaytestTools.RemoveResource(first, adjacent);
        var observer = new TurnBeginObserver();
        turns.AddTurnBeginListeners(observer);
        try
        {
            BusaraPlaytestTools.FinishResourceSetup();
            Assert.That(observer.Count, Is.EqualTo(1), "Setup must start the normal lifecycle only once.");
            Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.FinishResourceSetup());
            Assert.That(observer.Count, Is.EqualTo(1));
        }
        finally
        {
            turns.RemoveTurnBeginListener(observer);
        }
        foreach (Player player in manager.Players)
            AssertLegalSetup(player, required[player]);
        Assert.That(originalSlot.resource, Is.SameAs(preserved), "Legal partial placements must stay in their chosen spaces.");
        Assert.That(turns.ActivePlayer, Is.SameAs(first));
        Assert.That(ActionManager.Instance.CanPerformAction(), Is.True);
        Assert.That(BusaraPlaytestTools.EditBlockReason(), Is.Empty);

        phase = "Editing exact slots, resource stock and selection";
        CheckResources(first, second);
        phase = "Checking rejected arguments and unfinished actions";
        CheckGuards(first, second);
        // Empty boards are skipped by GameManager when a normal turn begins.
        Assert.That(BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 1), Is.EqualTo(1));
        Assert.That(BusaraPlaytestTools.AddResources(second, ResourceType.Earth, 1), Is.EqualTo(1));
        phase = "Editing kingdoms and visibility";
        Kingdom original = first.Kingdom;
        BusaraPlaytestTools.SetVisibility(first, true, true);
        Assert.That(first.kingdomRevealed && first.virtuesHidden, Is.True);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.SetKingdom(first, second.Kingdom));
        BusaraPlaytestTools.SetKingdom(first, second.Kingdom, true);
        Assert.That(first.Kingdom, Is.SameAs(second.Kingdom));
        Assert.That(first.kingdomRevealed, Is.False);
        BusaraPlaytestTools.SetKingdom(first, original);
        BusaraPlaytestTools.SetVisibility(first, false, false);
        Assert.That(first.kingdomRevealed || first.virtuesHidden, Is.False);

        phase = "Editing virtue stock";
        Virtue virtue = manager.kingdomCatalog.kingdoms.SelectMany(kingdom => kingdom.virtuesForWin)
            .Select(goal => goal.virtues).First(item => item != null);
        Assert.That(() => BusaraPlaytestTools.AddVirtues(first, virtue, 0), Throws.InstanceOf<ArgumentException>());
        Assert.That(() => BusaraPlaytestTools.RemoveVirtues(first, virtue.type, 0), Throws.InstanceOf<ArgumentException>());
        foreach (Player player in manager.Players)
            foreach (VirtueType type in Enum.GetValues(typeof(VirtueType)))
                BusaraPlaytestTools.RemoveVirtues(player, type, 100);
        Assert.That(BusaraPlaytestTools.AddVirtues(first, virtue, 100), Is.EqualTo(12));
        Assert.That(first.Virtues, Has.Count.EqualTo(12));
        Assert.That(BusaraPlaytestTools.AddVirtues(second, virtue, 1), Is.Zero);
        Assert.That(BusaraPlaytestTools.AddVirtues(second, virtue, 1, true), Is.EqualTo(1));
        Assert.That(second.Virtues, Has.Count.EqualTo(1));
        Assert.That(BusaraPlaytestTools.RemoveVirtues(first, virtue.type, 100), Is.EqualTo(12));
        Assert.That(first.Virtues, Is.Empty);
        Assert.That(BusaraPlaytestTools.RemoveVirtues(first, virtue.type, 1), Is.Zero);
        Assert.That(BusaraPlaytestTools.AddVirtues(first, virtue, 1), Is.EqualTo(1));
        Assert.That(turns.ActivePlayer, Is.SameAs(first), "Fixture edits must not consume turns.");

        phase = "Blocking fixture edits during a real modal";
        PowerManager.Instance.InspectPlayer(first);
        Assert.That(PowerManager.Instance.IsBusy, Is.True);
        Assert.That(BusaraPlaytestTools.EditBlockReason(true), Is.Not.Empty);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.AddVirtues(first, virtue, 1));
        PowerManager.Instance.Choices.Single(choice => choice.Label == "Continue").OnSelected();
        Assert.That(PowerManager.Instance.IsBusy, Is.False);
        yield return null;

        phase = "Queueing exact live cards without drawing";
        DeckManager deck = DeckManager.Instance;
        ResourceCard resourceCard = deck.Cards.OfType<ResourceCard>().Last();
        DisasterCard winterCard = deck.Cards.OfType<DisasterCard>()
            .First(card => card.effect is HardWinterDisaster);
        CheckReorder(deck, resourceCard);
        CheckReorder(deck, winterCard);
        int missing = deck.Cards.RemoveAll(card => card == resourceCard);
        deck.CardCount = Math.Max(0, deck.CardCount - missing);
        Card[] remaining = deck.Cards.ToArray();
        int remainingCount = deck.CardCount;
        BusaraPlaytestTools.ForceNextCard(resourceCard);
        Assert.That(deck.Cards, Is.EqualTo(new Card[] { resourceCard }.Concat(remaining)));
        Assert.That(deck.CardCount, Is.EqualTo(remainingCount + 1));
        CheckReorder(deck, resourceCard);
        Assert.That(deck.Cards.Count(card => card == resourceCard), Is.EqualTo(1));
        Assert.That(first.hasDrawnResource, Is.False);
        Assert.That(turns.ActivePlayer, Is.SameAs(first));

        phase = "Drawing and placing the queued resource";
        Resource[] beforeResources = Object.FindObjectsByType<Resource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        BusaraPlaytestTools.DrawNextCard();
        Resource drawn = Object.FindObjectsByType<Resource>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Except(beforeResources).Single();
        Assert.That(drawn.resourceType, Is.EqualTo(resourceCard.Resource));
        Assert.That(drawn.slot, Is.Null);
        Assert.That(first.hasDrawnResource, Is.True);
        Assert.That(BusaraPlaytestTools.EditBlockReason(true), Is.Not.Empty);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 1));
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.DrawNextCard());
        DrawResourceActionMove draw = Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include);
        Slot destination = first.Board.Slots.First(slot => !slot.isOccupied);
        draw.Onselection(drawn);
        draw.Onselection(destination);
        Assert.That(destination.resource, Is.SameAs(drawn));
        Assert.That(drawn.slot, Is.SameAs(destination));
        Assert.That(first.hasDrawnResource, Is.False);
        Assert.That(turns.ActivePlayer, Is.SameAs(second));
        BusaraPlaytestTools.EndTurn();
        Assert.That(turns.ActivePlayer, Is.SameAs(first));
        yield return null;

        phase = "Drawing queued hard winter through the actual draw action";
        BusaraPlaytestTools.ForceNextCard(winterCard);
        int beforeDraw = deck.CardCount;
        BusaraPlaytestTools.DrawNextCard();
        yield return null;
        Assert.That(HardWinterDisaster.Active, Is.SameAs(winterCard.effect));
        Assert.That(deck.Cards.Last(), Is.SameAs(winterCard), "Normal draws rotate cards back into the deck.");
        Assert.That(deck.CardCount, Is.EqualTo(beforeDraw - 1));
        Assert.That(turns.isSpecialCardDrawn, Is.True);
        Assert.That(BusaraPlaytestTools.EditBlockReason(true), Is.Not.Empty);
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.RemoveVirtues(first, virtue.type, 1));
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.ForceNextCard(resourceCard));
        Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.EndTurn());
        Assert.That(turns.ActivePlayer, Is.SameAs(first));
        Assert.That(HardWinterDisaster.Active.TryDiscard(first, virtue), Is.True);
        yield return null;
        Assert.That(turns.ActivePlayer, Is.SameAs(second));
        Assert.That(HardWinterDisaster.Active.TryDiscard(second, virtue), Is.True);
        yield return null;
        Assert.That(first.Virtues, Is.Empty);
        Assert.That(second.Virtues, Is.Empty);
        Assert.That(HardWinterDisaster.Active, Is.Null);
        Assert.That(turns.isSpecialCardDrawn, Is.False);
        Assert.That(turns.ActivePlayer, Is.SameAs(second), "Resume after the drawer, not after the final discard owner.");
        Assert.That(BusaraPlaytestTools.EditBlockReason(), Is.Empty);
        BusaraPlaytestTools.EndTurn();
        Assert.That(turns.ActivePlayer, Is.SameAs(first));

        phase = "Opening the Editor window";
        if (!EditorWindow.HasOpenInstances<BusaraPlaytestWindow>())
        {
            BusaraPlaytestWindow window = EditorWindow.GetWindow<BusaraPlaytestWindow>();
            try
            {
                window.Show();
                window.Repaint();
                yield return null;
                yield return null;
                using (var serialized = new SerializedObject(window))
                {
                    serialized.FindProperty("tab").intValue = 1;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                window.Repaint();
                yield return null;
                yield return null;
                Assert.That(window, Is.Not.Null);
            }
            finally
            {
                window.Close();
            }
        }
    }

    private static void AssertLegalSetup(Player player, ResourceType[] required)
    {
        Assert.That(player.hasFinishedSettingUp, Is.True);
        Assert.That(player.setUpCard, Is.Null);
        CollectionAssert.AreEquivalent(required, PowerRules.Resources(player).Select(piece => piece.resourceType));
        foreach (Slot slot in player.Board.GetOccupiedSlots())
            Assert.That(BoardManager.GetAdjacentSlots(slot).Any(neighbor =>
                player.Board.Slots.Contains(neighbor) && neighbor.isOccupied), Is.False,
                player.Name + " has adjacent setup resources at " + slot.Index);
    }

    private static void CheckResources(Player first, Player second)
    {
        Slot chosen = first.Board.Slots.Last(slot => !slot.isOccupied);
        Assert.That(BusaraPlaytestTools.AddResources(first, ResourceType.Water, 1, chosen), Is.EqualTo(1));
        Resource piece = chosen.resource;
        Assert.That(chosen.isOccupied, Is.True);
        Assert.That(piece.resourceType, Is.EqualTo(ResourceType.Water));
        Assert.That(piece.slot, Is.SameAs(chosen));
        first.selectedResources.Add(piece);
        first.selectedSlots.Add(chosen);
        BusaraPlaytestTools.RemoveResource(first, chosen);
        Assert.That(chosen.isOccupied, Is.False);
        Assert.That(chosen.resource, Is.Null);
        Assert.That(piece == null, Is.True, "Fixture removal must destroy immediately, without a frame delay.");
        Assert.That(first.selectedResources, Is.Empty);
        Assert.That(first.selectedSlots, Is.Empty);
        foreach (Player player in new[] { first, second })
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                BusaraPlaytestTools.RemoveResources(player, type, 100);
        Assert.That(first.Board.Slots.Count + second.Board.Slots.Count, Is.GreaterThan(20));
        Slot[] firstEmpty = first.Board.Slots.ToArray();
        int added = BusaraPlaytestTools.AddResources(first, ResourceType.Fire, 100);
        Assert.That(added, Is.EqualTo(Math.Min(20, firstEmpty.Length)));
        Assert.That(firstEmpty.Take(added).All(slot => slot.isOccupied && slot.resource.resourceType == ResourceType.Fire), Is.True);
        Assert.That(BusaraPlaytestTools.AddResources(second, ResourceType.Fire, 100), Is.EqualTo(20 - added));
        Assert.That(BusaraPlaytestTools.AddResources(second, ResourceType.Fire, 1), Is.Zero);
        Assert.That(BusaraPlaytestTools.AddResources(second, ResourceType.Fire, 1, null, true), Is.EqualTo(1));
        foreach (Player player in new[] { first, second })
        {
            Resource[] owned = player.Board.GetOccupiedSlots().Select(slot => slot.resource).ToArray();
            Assert.That(BusaraPlaytestTools.RemoveResources(player, ResourceType.Fire, 100), Is.EqualTo(owned.Length));
            Assert.That(owned.All(resource => resource == null), Is.True);
            Assert.That(player.Board.Slots.All(slot => !slot.isOccupied && slot.resource == null), Is.True);
        }
        Assert.That(BusaraPlaytestTools.RemoveResources(first, ResourceType.Fire, 1), Is.Zero);
    }

    private static void CheckGuards(Player first, Player second)
    {
        Assert.That(() => BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 0),
            Throws.InstanceOf<ArgumentException>());
        Assert.That(() => BusaraPlaytestTools.RemoveResources(first, ResourceType.Earth, 0),
            Throws.InstanceOf<ArgumentException>());
        Assert.Throws<ArgumentException>(() => BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 1, second.Board.Slots[0]));
        Assert.Throws<ArgumentException>(() => BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 2, first.Board.Slots[0]));
        Assert.Throws<ArgumentException>(() => BusaraPlaytestTools.RemoveResource(first, second.Board.Slots[0]));
        var foreignObject = new GameObject("Non-participating playtest player");
        try
        {
            Player foreign = foreignObject.AddComponent<Player>();
            Assert.Throws<ArgumentException>(() => BusaraPlaytestTools.AddResources(foreign, ResourceType.Earth, 1));
        }
        finally
        {
            Object.DestroyImmediate(foreignObject);
        }
        ActionManager.Instance.SetAction(ActionManager.ActionState.MoveResource);
        try
        {
            Assert.That(BusaraPlaytestTools.EditBlockReason(true), Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => BusaraPlaytestTools.AddResources(first, ResourceType.Earth, 1));
        }
        finally
        {
            ActionManager.Instance.ResetActionState();
        }
    }

    private static void CheckReorder(DeckManager deck, Card card)
    {
        var before = deck.Cards.ToList();
        before.Remove(card);
        int count = deck.CardCount;
        BusaraPlaytestTools.ForceNextCard(card);
        Assert.That(deck.Cards[0], Is.SameAs(card));
        Assert.That(deck.Cards, Is.EqualTo(new[] { card }.Concat(before)));
        Assert.That(deck.CardCount, Is.EqualTo(count));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
            Debug.Log("Playtest scene failure (" + phase + "): " + TestContext.CurrentContext.Result.StackTrace);
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

    private class TurnBeginObserver : TurnManager.TurnBeginListener
    {
        public int Count;
        public void OnTurnBegin() { Count++; }
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
