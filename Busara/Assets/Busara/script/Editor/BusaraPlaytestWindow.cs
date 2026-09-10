using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class BusaraPlaytestWindow : EditorWindow
{
    [SerializeField] private Player player;
    [SerializeField] private Slot slot;
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private Virtue virtue;
    [SerializeField] private Kingdom kingdom;
    [SerializeField] private Card nextCard;
    [SerializeField] private int quantity = 1;
    [SerializeField] private bool ignoreStock;
    [SerializeField] private bool duplicateKingdoms;
    [SerializeField] private Vector2 scroll;
    [SerializeField] private int tab;
    [SerializeField] private Vector2 setupScroll;
    [SerializeField] private string lastFastStartCompletion;
    [SerializeField] private List<PlayerSetupEntry> setupPlayers = new List<PlayerSetupEntry>
    {
        new PlayerSetupEntry("Player 1"),
        new PlayerSetupEntry("Player 2")
    };
    private Virtue[] virtues = Array.Empty<Virtue>();
    private string message = "Choose a player to build a test scenario.";
    private MessageType messageType = MessageType.Info;

    [MenuItem("Tools/Busara/Playtest Runner")]
    public static void Open()
    {
        GetWindow<BusaraPlaytestWindow>("Busara Playtest").Show();
    }

    private void OnEnable()
    {
        minSize = new Vector2(430, 500);
        virtues = AssetDatabase.FindAssets("t:Virtue", new[] { "Assets/Busara" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<Virtue>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).OrderBy(asset => asset.type).ToArray();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        string completed = BusaraFastTestStart.CompletedRequestId;
        if (Application.isPlaying && PlayerManager.Instance != null && PlayerManager.Instance.IsSetupComplete &&
            !string.IsNullOrEmpty(completed) && completed != lastFastStartCompletion)
        {
            lastFastStartCompletion = completed;
            tab = 1;
            player = null;
        }
        EditorGUILayout.LabelField("Busara Playtest Runner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Live fixture controls, not gameplay actions. Changes last only for this Play session. " +
            "This window intentionally shows hidden information. No scene/asset saves or Undo.", MessageType.Info);
        DrawEditorControls();
        tab = GUILayout.Toolbar(tab, new[] { "Game Setup", "Gameplay" });
        EditorGUILayout.HelpBox(message, messageType);
        if (!string.IsNullOrEmpty(BusaraFastTestStart.Status))
            EditorGUILayout.HelpBox(BusaraFastTestStart.Status,
                BusaraFastTestStart.Failed ? MessageType.Error : MessageType.Info);
        if (BusaraFastTestStart.IsPending && GUILayout.Button("Cancel Fast Test Start"))
            BusaraFastTestStart.Cancel();
        if (tab == 0)
            DrawGameSetup();
        else
            DrawGameplay();
    }

    private void DrawGameSetup()
    {
        setupScroll = EditorGUILayout.BeginScrollView(setupScroll);
        PlayerManager manager = Object.FindFirstObjectByType<PlayerManager>();
        if (manager == null)
        {
            EditorGUILayout.HelpBox("Open GameScene to choose the player count, names and kingdoms here, then enter Play Mode.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }
        if (Application.isPlaying && manager.IsSetupComplete)
        {
            EditorGUILayout.HelpBox("Players are configured. Finish resource setup below, then use the Gameplay tab. " +
                "Stop and re-enter Play Mode to change the player count.", MessageType.Info);
            foreach (Player participant in manager.Players)
                EditorGUILayout.LabelField(participant.Name, participant.Kingdom.kingdomName +
                    (participant.IsBotControlled ? " / Bot controlled" : " / Player controlled"));
            string reason = BusaraPlaytestTools.EditBlockReason(true);
            if (!string.IsNullOrEmpty(reason))
                EditorGUILayout.HelpBox(reason, MessageType.Warning);
            using (new EditorGUI.DisabledScope(BusaraFastTestStart.IsPending || !string.IsNullOrEmpty(reason) ||
                manager.Players.All(participant => participant.hasFinishedSettingUp)))
                if (GUILayout.Button("Finish Resource Setup (legal non-adjacent placement)"))
                    Run(() =>
                    {
                        BusaraPlaytestTools.FinishResourceSetup();
                        message = "Resource setup completed using each player's setup card and non-adjacent spaces.";
                        tab = 1;
                    });
            EditorGUILayout.EndScrollView();
            return;
        }
        int capacity = manager.SupportedPlayerCount;
        if (capacity < PlayerSetupRules.MinimumPlayers)
        {
            EditorGUILayout.HelpBox("This scene needs at least two configured player boards.", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }
        int count = EditorGUILayout.IntSlider("Number of players", Mathf.Clamp(setupPlayers.Count,
            PlayerSetupRules.MinimumPlayers, capacity), PlayerSetupRules.MinimumPlayers, capacity);
        while (setupPlayers.Count < count)
            setupPlayers.Add(new PlayerSetupEntry("Player " + (setupPlayers.Count + 1)));
        if (setupPlayers.Count > count)
            setupPlayers.RemoveRange(count, setupPlayers.Count - count);
        Kingdom[] catalog = manager.kingdomCatalog == null || manager.kingdomCatalog.kingdoms == null ?
            Array.Empty<Kingdom>() : manager.kingdomCatalog.kingdoms.ToArray();
        string[] options = new[] { "Choose a kingdom..." }.Concat(catalog.Select(item =>
            item == null ? "Missing kingdom" : item.kingdomName)).ToArray();
        for (int i = 0; i < setupPlayers.Count; i++)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Player " + (i + 1), EditorStyles.boldLabel);
            PlayerSetupEntry entry = setupPlayers[i];
            entry.Name = EditorGUILayout.TextField("Name", entry.Name);
            int index = EditorGUILayout.Popup("Kingdom", Array.IndexOf(catalog, entry.Kingdom) + 1, options);
            entry.Kingdom = index == 0 ? null : catalog[index - 1];
            entry.IsBotControlled = EditorGUILayout.Popup("Control", entry.IsBotControlled ? 1 : 0,
                new[] { "Player controlled", "Bot controlled" }) == 1;
        }
        bool valid = PlayerSetupRules.Validate(setupPlayers, manager.kingdomCatalog, capacity, out string error);
        EditorGUILayout.HelpBox(valid ? "Ready. Use Fast Test Start, or enter Play Mode and press Start Game here." : error,
            valid ? MessageType.Info : MessageType.Warning);
        bool ready = Application.isPlaying && Object.FindFirstObjectByType<PlayerSetupUI>() != null;
        using (new EditorGUI.DisabledScope(!valid || !ready || BusaraFastTestStart.IsPending))
            if (GUILayout.Button("Start Game"))
                Run(() =>
                {
                    BusaraPlaytestTools.StartGame(setupPlayers);
                    message = "Game started. Place initial resources in-game or finish their legal placement here.";
                    player = null;
                });
        using (new EditorGUI.DisabledScope(!valid || BusaraFastTestStart.IsPending ||
            EditorApplication.isCompiling || EditorApplication.isUpdating))
            if (GUILayout.Button("Fast Test Start"))
                Run(() => BusaraFastTestStart.Begin(setupPlayers));
        EditorGUILayout.EndScrollView();
    }

    private void DrawGameplay()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (GUILayout.Button("Bot Decisions"))
            BusaraBotWindow.Open();

        PlayerManager players = PlayerManager.Instance;
        Player[] participants = players == null || players.Players == null ? Array.Empty<Player>() :
            players.Players.Where(item => item != null && item.Board != null).ToArray();
        if (participants.Length == 0)
        {
            EditorGUILayout.HelpBox("Use Game Setup to open GameScene, enter Play Mode and configure players.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }
        if (players.Players.Any(item => item == null || item.Board == null || item.Virtues == null ||
            item.Board.Slots == null || item.Board.Slots.Contains(null)))
        {
            EditorGUILayout.HelpBox("Repair missing player, inventory or board-slot references in the scene first.", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }
        if (!participants.Contains(player))
            SelectPlayer(participants.FirstOrDefault(item => TurnManager.Instance != null && item == TurnManager.Instance.ActivePlayer)
                ?? participants[0]);
        int selected = Array.IndexOf(participants, player);
        int chosen = EditorGUILayout.Popup("Player", selected, participants.Select(item =>
            item.Name + (TurnManager.Instance != null && item == TurnManager.Instance.ActivePlayer ? " [active]" : "")).ToArray());
        if (chosen != selected)
            SelectPlayer(participants[chosen]);

        DrawStatus();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Player"))
                Selection.activeGameObject = player.gameObject;
            if (GUILayout.Button("Select Board"))
                Selection.activeGameObject = player.Board.gameObject;
        }
        string blocked = BusaraPlaytestTools.EditBlockReason(true);
        if (!string.IsNullOrEmpty(blocked))
            EditorGUILayout.HelpBox(blocked, MessageType.Warning);
        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(blocked)))
        {
            quantity = Mathf.Clamp(EditorGUILayout.IntField("Quantity", quantity), 1, 100);
            ignoreStock = EditorGUILayout.ToggleLeft("Ignore stock limits (out-of-rules fixture)", ignoreStock);
            DrawResources();
            DrawVirtues();
            DrawKingdom(players);
        }
        DrawDeck();
        DrawTurnControls();
        EditorGUILayout.EndScrollView();
    }

    private void DrawEditorControls()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Open GameScene") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
                if (GUILayout.Button("Play"))
                    EditorApplication.isPlaying = true;
            }
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button(EditorApplication.isPaused ? "Resume" : "Pause"))
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                using (new EditorGUI.DisabledScope(!EditorApplication.isPaused))
                    if (GUILayout.Button("Step"))
                        EditorApplication.Step();
                if (GUILayout.Button("Stop") && EditorUtility.DisplayDialog("End play session?",
                    "All runtime fixture edits will be lost. Saved scenes and assets are not changed.", "Stop", "Cancel"))
                    EditorApplication.isPlaying = false;
            }
        }
    }

    private void DrawStatus()
    {
        EditorGUILayout.LabelField("Player control", player.IsBotControlled ? "Bot controlled" : "Player controlled");
        TurnManager turns = TurnManager.Instance;
        PowerManager powers = PowerManager.Instance;
        EditorGUILayout.LabelField("Turn / action", (turns != null && turns.ActivePlayer != null ? turns.ActivePlayer.Name : "Not started")
            + " / " + BusaraPlaytestTools.ActionStateName);
        EditorGUILayout.LabelField("Control / pending", (powers != null && powers.Controller != null ? powers.Controller.Name : "Turn owner")
            + (turns != null && turns.isSpecialCardDrawn ? " / special discard" : "")
            + (powers != null && powers.IsBusy ? " / " + powers.ChoiceTitle : ""));
        EditorGUILayout.LabelField("Kingdom", player.Kingdom != null ? player.Kingdom.kingdomName : "Not assigned");
        if (player.Kingdom != null && player.Kingdom.power != null)
        {
            Power power = player.Kingdom.power;
            EditorGUILayout.LabelField("Power", $"{power.powerName} / cost {power.virtueCost} / {power.timing}");
            if (player.Kingdom.virtuesForWin != null)
                EditorGUILayout.LabelField("Victory goals", string.Join(", ", player.Kingdom.virtuesForWin
                    .Where(goal => goal != null && goal.virtues != null)
                    .Select(goal => $"{goal.NumberofVirtues} {goal.virtues.type}")));
        }
        EditorGUILayout.LabelField("Setup / board", (player.hasFinishedSettingUp ? "Finished" : "Resource setup")
            + $" / {PowerRules.Resources(player).Count} occupied, {PowerRules.EmptySlots(player).Count} empty");
    }

    private void DrawResources()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Resources and exact board spaces", EditorStyles.boldLabel);
        resourceType = (ResourceType)EditorGUILayout.EnumPopup("Resource", resourceType);
        EditorGUILayout.LabelField("Owned / stock", $"{PowerRules.Resources(player).Count(piece => piece.resourceType == resourceType)} / " +
            PowerRules.ResourceStock(resourceType));
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add to empty spaces"))
                Run(() => Applied(BusaraPlaytestTools.AddResources(player, resourceType, quantity, null, ignoreStock), "resources added"));
            if (GUILayout.Button("Remove by type"))
                Run(() => Applied(BusaraPlaytestTools.RemoveResources(player, resourceType, quantity), "resources removed"));
        }
        if (GUILayout.Button("Clear this board...") && EditorUtility.DisplayDialog("Clear board?",
            "Remove every resource from " + player.Name + "'s runtime board?", "Clear", "Cancel"))
            Run(() =>
            {
                int removed = 0;
                foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                    removed += BusaraPlaytestTools.RemoveResources(player, type, 100);
                message = $"Removed {removed} resources.";
            });

        var slots = player.Board.Slots;
        for (int start = 0; start < slots.Count; start += 4)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int i = start; i < Math.Min(start + 4, slots.Count); i++)
                {
                    Slot item = slots[i];
                    string label = (item == slot ? "> " : "") + (item.Index + 1) + "\n" +
                        (item.resource != null ? item.resource.resourceType.ToString() : "Empty");
                    if (GUILayout.Button(label, GUILayout.Height(38)))
                    {
                        slot = item;
                        Selection.activeGameObject = item.gameObject;
                    }
                }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(slot == null || !slots.Contains(slot) || slot.isOccupied))
                if (GUILayout.Button("Add 1 at selected space"))
                    Run(() => message = $"Added {BusaraPlaytestTools.AddResources(player, resourceType, 1, slot, ignoreStock)} resource.");
            using (new EditorGUI.DisabledScope(slot == null || !slots.Contains(slot) || !slot.isOccupied))
                if (GUILayout.Button("Remove selected piece"))
                    Run(() => { BusaraPlaytestTools.RemoveResource(player, slot); message = "Removed selected piece."; });
        }
        EditorGUILayout.HelpBox("Space numbers are the real board slot indices. This grid is a selector, not the board's adjacency layout.", MessageType.None);
    }

    private void DrawVirtues()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Virtues (including hidden inventory)", EditorStyles.boldLabel);
        foreach (VirtueType type in Enum.GetValues(typeof(VirtueType)))
            EditorGUILayout.LabelField(type.ToString(), player.Virtues.Count(item => item != null && item.type == type).ToString());
        if (virtues.Length == 0)
        {
            EditorGUILayout.HelpBox("No virtue assets were found under Assets/Busara.", MessageType.Error);
            return;
        }
        int index = Mathf.Max(0, Array.IndexOf(virtues, virtue));
        virtue = virtues[EditorGUILayout.Popup("Virtue", index, virtues.Select(item => item.type.ToString()).ToArray())];
        EditorGUILayout.LabelField("Available virtue stock", PowerRules.VirtueStock(virtue).ToString());
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add virtues"))
                Run(() => Applied(BusaraPlaytestTools.AddVirtues(player, virtue, quantity, ignoreStock), "virtues added"));
            if (GUILayout.Button("Remove virtues"))
                Run(() => Applied(BusaraPlaytestTools.RemoveVirtues(player, virtue.type, quantity), "virtues removed"));
        }
        if (GUILayout.Button("Clear this player's virtues...") && EditorUtility.DisplayDialog("Clear virtues?",
            "Remove every virtue from " + player.Name + "?", "Clear", "Cancel"))
            Run(() =>
            {
                foreach (VirtueType type in Enum.GetValues(typeof(VirtueType)))
                    while (player.Virtues.Any(item => item.type == type))
                        BusaraPlaytestTools.RemoveVirtues(player, type, 100);
                message = "Cleared this player's virtues.";
            });
    }

    private void DrawKingdom(PlayerManager players)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kingdom and visibility fixtures", EditorStyles.boldLabel);
        Kingdom[] catalog = players.kingdomCatalog == null ? Array.Empty<Kingdom>() :
            players.kingdomCatalog.kingdoms.Where(item => item != null).ToArray();
        if (catalog.Length > 0)
        {
            int index = Mathf.Max(0, Array.IndexOf(catalog, kingdom));
            kingdom = catalog[EditorGUILayout.Popup("Kingdom", index, catalog.Select(item => item.kingdomName).ToArray())];
            duplicateKingdoms = EditorGUILayout.ToggleLeft("Allow duplicate kingdoms (reaction scenarios)", duplicateKingdoms);
            if (GUILayout.Button("Assign kingdom (hidden)"))
                Run(() => { BusaraPlaytestTools.SetKingdom(player, kingdom, duplicateKingdoms); message = "Assigned kingdom; asset unchanged."; });
        }
        bool revealed = EditorGUILayout.Toggle("Kingdom revealed", player.kingdomRevealed);
        bool hidden = EditorGUILayout.Toggle("Virtues hidden", player.virtuesHidden);
        if (revealed != player.kingdomRevealed || hidden != player.virtuesHidden)
            Run(() => { BusaraPlaytestTools.SetVisibility(player, revealed, hidden); message = "Updated visibility."; });
    }

    private void DrawDeck()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Choose the next draw", EditorStyles.boldLabel);
        DeckManager deck = DeckManager.Instance;
        if (deck == null || deck.Cards == null)
        {
            EditorGUILayout.HelpBox("No initialized deck in this scene.", MessageType.Info);
            return;
        }
        EditorGUILayout.LabelField("Entries / draw counter", $"{deck.Cards.Count} / {deck.CardCount}");
        EditorGUILayout.LabelField("Next card", deck.Cards.Count > 0 ? CardLabel(deck.Cards[0]) : "Empty");
        Card[] choices = Object.FindObjectsByType<Card>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(card => card.gameObject.scene == deck.gameObject.scene &&
                (card is ResourceCard || card is DisasterCard)).OrderBy(CardLabel).ToArray();
        if (choices.Length > 0)
        {
            int index = Mathf.Max(0, Array.IndexOf(choices, nextCard));
            nextCard = choices[EditorGUILayout.Popup("Card to queue", index, choices.Select(CardLabel).ToArray())];
            using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(BusaraPlaytestTools.EditBlockReason())))
            {
                if (GUILayout.Button("Make this the next card"))
                    Run(() => { BusaraPlaytestTools.ForceNextCard(nextCard); message = "Queued " + CardLabel(nextCard) + ". It has not been drawn."; });
                if (GUILayout.Button("Draw next card through normal gameplay"))
                    Run(() => { BusaraPlaytestTools.DrawNextCard(); message = "Draw invoked. Place the resource or resolve the disaster in the game."; });
            }
        }
        EditorGUILayout.HelpBox("Queuing preserves the order of other cards. A scene card missing from the deck is inserted once. " +
            "Drawing still uses normal protection, placement, discard and turn logic.", MessageType.None);
        foreach (Card card in deck.Cards.Take(8))
            EditorGUILayout.LabelField("  " + CardLabel(card));
    }

    private void DrawTurnControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scenario controls", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(BusaraPlaytestTools.EditBlockReason(true))))
        {
            if (GUILayout.Button("Show all-player virtue popup"))
                Run(() =>
                {
                    if (DisplayManager.Instance == null || !DisplayManager.Instance.ShowPlayerInfo())
                        throw new InvalidOperationException("The player-info popup is not bound.");
                    message = "Opened player info.";
                });
        }
        using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(BusaraPlaytestTools.EditBlockReason())))
        {
            if (GUILayout.Button("End current turn (normal reactions apply)"))
                Run(() => { BusaraPlaytestTools.EndTurn(); message = "Turn completion requested; resolve any reaction prompts in-game."; });
            if (GUILayout.Button("Evaluate victory now"))
                Run(() =>
                {
                    if (GameManager.Instance == null)
                        throw new InvalidOperationException("GameManager is not available.");
                    GameManager.Instance.CheckWinConditions();
                    message = "Evaluated victory using the game's normal rules.";
                });
        }
    }

    private void SelectPlayer(Player value)
    {
        player = value;
        slot = null;
        kingdom = value.Kingdom;
    }

    private void Applied(int count, string description)
    {
        message = $"{count}/{quantity} {description}. Available stock, owned pieces and empty spaces limit the result.";
    }

    private void Run(Action action)
    {
        try
        {
            action();
            messageType = MessageType.Info;
        }
        catch (ArgumentException exception)
        {
            Report(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            Report(exception.Message);
        }
    }

    private void Report(string error)
    {
        message = error;
        messageType = MessageType.Error;
        Debug.LogWarning("Busara Playtest: " + error);
    }

    private static string CardLabel(Card card)
    {
        if (card == null) return "Missing card";
        return card is ResourceCard resource ? "Resource: " + resource.Resource + " - " + card.CardName :
            "Disaster: " + card.CardName;
    }
}
