using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BusaraPlaytestTools
{
    public static void StartGame(IReadOnlyList<PlayerSetupEntry> entries)
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode before starting the game.");
        PlayerSetupUI setup = Object.FindFirstObjectByType<PlayerSetupUI>();
        if (setup == null)
            throw new InvalidOperationException("Open GameScene and wait for player setup to initialize.");
        if (!setup.TryStartGame(entries, out string error))
            throw new InvalidOperationException(error);
    }

    public static string ActionStateName => ActionManager.Instance == null ? "Unavailable" : ActionState().ToString();

    public static string EditBlockReason(bool allowSetup = false)
    {
        if (!Application.isPlaying)
            return "Enter Play Mode. These controls never edit saved scene or asset data.";
        if (PlayerManager.Instance == null || TurnManager.Instance == null ||
            ActionManager.Instance == null || BoardManager.Instance == null || SelectionManager.Instance == null)
            return "Open GameScene and wait for its managers to initialize.";
        if (PlayerManager.Instance.IsAwaitingSetup)
            return "Choose players and kingdoms in the game's setup menu, then press Start Game.";
        if (PlayerManager.Instance.Players == null || PlayerManager.Instance.Players.Count == 0 ||
            PlayerManager.Instance.Players.Any(player => player == null || player.Board == null ||
                player.Virtues == null || player.Board.Slots == null || player.Board.Slots.Contains(null)))
            return "The scene needs valid participating players, inventories and board slots.";
        if (TurnManager.Instance.ActivePlayer == null)
            return "Wait for the first player turn.";
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
            return "Finish the pending power/reaction choice in the game first.";
        if (TurnManager.Instance.isSpecialCardDrawn || HardWinterDisaster.Active != null)
            return "Finish the disaster/weapon discard in the game first.";
        if (PlayerManager.Instance.Players.Any(player => player.hasDrawnResource))
            return "Place the drawn resource before changing the fixture.";
        var state = ActionState();
        if (state != ActionManager.ActionState.None &&
            !(allowSetup && state == ActionManager.ActionState.ResourceSetup))
            return "Finish the current action before changing the fixture: " + state;
        if (!allowSetup && PlayerManager.Instance.Players.Any(player => !player.hasFinishedSettingUp))
            return "Finish resource setup, or use Finish Resource Setup in this window.";
        return "";
    }

    public static int AddResources(Player player, ResourceType type, int count, Slot chosenSlot = null, bool ignoreStock = false)
    {
        RequirePlayer(player);
        RequireCount(count);
        if (!Enum.IsDefined(typeof(ResourceType), type))
            throw new ArgumentException("Choose a valid resource type.");
        if (chosenSlot != null && (count != 1 || !player.Board.Slots.Contains(chosenSlot) ||
            chosenSlot.isOccupied || chosenSlot.resource != null))
            throw new ArgumentException("An exact-slot addition requires quantity 1 and an empty slot on this player's board.");
        var slots = chosenSlot == null ? PowerRules.EmptySlots(player) : new System.Collections.Generic.List<Slot> { chosenSlot };
        int applied = Math.Min(count, Math.Min(slots.Count, ignoreStock ? count : PowerRules.ResourceStock(type)));
        for (int i = 0; i < applied; i++)
        {
            Resource resource = BoardManager.Instance.SpawnByResourceType(type, slots[i].gameObject).GetComponent<Resource>();
            Board.PlaceResource(resource, slots[i]);
        }
        if (applied > 0)
            Refresh();
        return applied;
    }

    public static int RemoveResources(Player player, ResourceType type, int count)
    {
        RequirePlayer(player);
        RequireCount(count);
        if (!Enum.IsDefined(typeof(ResourceType), type))
            throw new ArgumentException("Choose a valid resource type.");
        Resource[] resources = PowerRules.Resources(player).Where(resource => resource.resourceType == type).Take(count).ToArray();
        if (resources.Length == 0)
            return 0;
        ClearSelections();
        foreach (Resource resource in resources)
            RemovePiece(resource);
        Refresh();
        return resources.Length;
    }

    public static void RemoveResource(Player player, Slot slot)
    {
        RequirePlayer(player);
        if (slot == null || !player.Board.Slots.Contains(slot) || !slot.isOccupied || slot.resource == null)
            throw new ArgumentException("Choose an occupied slot on this player's board.");
        ClearSelections();
        RemovePiece(slot.resource);
        Refresh();
    }

    public static int AddVirtues(Player player, Virtue virtue, int count, bool ignoreStock = false)
    {
        RequirePlayer(player);
        RequireCount(count);
        if (virtue == null || !Enum.IsDefined(typeof(VirtueType), virtue.type))
            throw new ArgumentException("Choose a virtue asset.");
        int applied = Math.Min(count, ignoreStock ? count : PowerRules.VirtueStock(virtue));
        for (int i = 0; i < applied; i++)
            player.Virtues.Add(virtue);
        if (applied > 0)
            Refresh();
        return applied;
    }

    public static int RemoveVirtues(Player player, VirtueType type, int count)
    {
        RequirePlayer(player);
        RequireCount(count);
        if (!Enum.IsDefined(typeof(VirtueType), type))
            throw new ArgumentException("Choose a valid virtue type.");
        var owned = player.Virtues.Where(virtue => virtue != null && virtue.type == type).Take(count).ToArray();
        foreach (Virtue virtue in owned)
            player.Virtues.Remove(virtue);
        if (owned.Length > 0)
            Refresh();
        return owned.Length;
    }

    public static void ForceNextCard(Card card)
    {
        RequireEditable();
        DeckManager deck = DeckManager.Instance;
        if (deck == null || deck.Cards == null)
            throw new InvalidOperationException("The deck is not initialized.");
        if (card == null || EditorUtility.IsPersistent(card) || card.gameObject.scene != deck.gameObject.scene ||
            (!(card is ResourceCard) && !(card is DisasterCard)) || (card is DisasterCard disaster && disaster.effect == null))
            throw new ArgumentException("Choose a live resource card or a disaster card with an effect from this game scene.");
        int index = deck.Cards.IndexOf(card);
        if (index >= 0)
            deck.Cards.RemoveAt(index);
        else
            deck.CardCount++;
        deck.Cards.Insert(0, card);
        Refresh();
    }

    public static void DrawNextCard()
    {
        RequireEditable();
        if (DeckManager.Instance == null || DeckManager.Instance.Cards == null ||
            DeckManager.Instance.Cards.Count == 0 || DeckManager.Instance.Cards[0] == null)
            throw new InvalidOperationException("Queue a card before drawing.");
        DrawResourceActionMove draw = Object.FindFirstObjectByType<DrawResourceActionMove>(FindObjectsInactive.Include);
        if (draw == null)
            throw new InvalidOperationException("The game's Draw action is not available.");
        draw.OnTapDraw();
    }

    public static void FinishResourceSetup()
    {
        RequireEditable(true);
        Player[] players = PlayerManager.Instance.Players.ToArray();
        if (players.All(player => player.hasFinishedSettingUp))
            throw new InvalidOperationException("Resource setup is already complete.");
        var placements = new Dictionary<Player, List<Slot>>();
        var resources = new Dictionary<Player, List<ResourceType>>();
        var stock = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>()
            .ToDictionary(type => type, PowerRules.ResourceStock);
        // Plan every board before mutating anything, so invalid partial setups remain untouched.
        foreach (Player player in players.Where(item => !item.hasFinishedSettingUp))
        {
            RequirePlayer(player);
            if (player.setUpCard == null || player.setUpCard.collectionResources == null)
                throw new InvalidOperationException(player.Name + " needs a resource setup card.");
            var remaining = new List<ResourceType>(player.setUpCard.collectionResources);
            foreach (Resource piece in PowerRules.Resources(player))
            {
                if (!remaining.Remove(piece.resourceType))
                    throw new InvalidOperationException(player.Name + " has resources beyond their setup card. Remove the extra pieces first.");
                if (BoardManager.GetAdjacentSlots(piece.slot).Any(neighbor =>
                    player.Board.Slots.Contains(neighbor) && neighbor.isOccupied))
                    throw new InvalidOperationException(player.Name + " has adjacent setup resources. Remove or reposition them first.");
            }
            List<Slot> candidates = PowerRules.EmptySlots(player).Where(slot =>
                !BoardManager.GetAdjacentSlots(slot).Any(neighbor =>
                    player.Board.Slots.Contains(neighbor) && neighbor.isOccupied)).ToList();
            var chosen = new List<Slot>();
            if (!FindSetupSlots(candidates, remaining.Count, chosen))
                throw new InvalidOperationException(player.Name + " has insufficient non-adjacent setup spaces. Reposition existing pieces first.");
            foreach (ResourceType type in remaining)
            {
                if (!stock.ContainsKey(type) || stock[type] <= 0)
                    throw new InvalidOperationException("Insufficient " + type + " stock to finish resource setup.");
                stock[type]--;
            }
            placements.Add(player, chosen);
            resources.Add(player, remaining);
        }
        foreach (Player player in resources.Keys)
        {
            for (int i = 0; i < resources[player].Count; i++)
                AddResources(player, resources[player][i], 1, placements[player][i]);
            player.hasFinishedSettingUp = true;
            player.setUpCard = null;
        }
        ClearSelections();
        foreach (ResourceCollectionDisplay display in Object.FindObjectsByType<ResourceCollectionDisplay>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            display.CheckForCompletion();
        ActionManager.Instance.ResetActionState();
        Player active = TurnManager.Instance.ActivePlayer;
        TurnManager.Instance.TriggerTurnBeginListeners();
        if (TurnManager.Instance.ActivePlayer == active && PowerManager.Instance != null)
            PowerManager.Instance.BeginNormalTurn(active);
    }

    private static bool FindSetupSlots(List<Slot> candidates, int needed, List<Slot> chosen)
    {
        if (needed == 0)
            return true;
        for (int i = 0; i <= candidates.Count - needed; i++)
        {
            Slot slot = candidates[i];
            var neighbors = BoardManager.GetAdjacentSlots(slot);
            chosen.Add(slot);
            if (FindSetupSlots(candidates.Skip(i + 1).Where(candidate => !neighbors.Contains(candidate)).ToList(),
                needed - 1, chosen))
                return true;
            chosen.RemoveAt(chosen.Count - 1);
        }
        return false;
    }

    public static void SetKingdom(Player player, Kingdom kingdom, bool allowDuplicate = false)
    {
        RequirePlayer(player);
        KingdomCatalog catalog = PlayerManager.Instance.kingdomCatalog;
        if (kingdom == null || kingdom.power == null || catalog == null || !catalog.kingdoms.Contains(kingdom))
            throw new ArgumentException("Choose a complete kingdom from this game's catalog.");
        if (!allowDuplicate && PlayerManager.Instance.Players.Any(other => other != player && other.Kingdom == kingdom))
            throw new InvalidOperationException("Another player owns that kingdom. Enable duplicate kingdoms only for deliberate reaction testing.");
        player.Kingdom = kingdom;
        player.kingdomRevealed = false;
        Refresh();
    }

    public static void SetVisibility(Player player, bool kingdomRevealed, bool virtuesHidden)
    {
        RequirePlayer(player);
        player.kingdomRevealed = kingdomRevealed;
        player.virtuesHidden = virtuesHidden;
        Refresh();
    }

    public static void EndTurn()
    {
        RequireEditable();
        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    }

    private static ActionManager.ActionState ActionState()
    {
        using (var serialized = new SerializedObject(ActionManager.Instance))
            return (ActionManager.ActionState)serialized.FindProperty("_currentActionState").enumValueIndex;
    }

    private static void RequireEditable(bool allowSetup = false)
    {
        string reason = EditBlockReason(allowSetup);
        if (!string.IsNullOrEmpty(reason))
            throw new InvalidOperationException(reason);
    }

    private static void RequirePlayer(Player player)
    {
        RequireEditable(true);
        if (player == null || !PlayerManager.Instance.Players.Contains(player) || player.Board == null ||
            player.Board.Slots == null || player.Board.Slots.Count == 0 || player.Virtues == null)
            throw new ArgumentException("Choose a participating player with an initialized board and inventory.");
        if (player.Board.Slots.Any(slot => slot == null || slot.isOccupied != (slot.resource != null) ||
            (slot.resource != null && slot.resource.slot != slot)))
            throw new InvalidOperationException("The board contains inconsistent slots. Repair the fixture before adding or removing pieces.");
    }

    private static void RequireCount(int count)
    {
        if (count < 1 || count > 100)
            throw new ArgumentOutOfRangeException(nameof(count), "Choose a quantity from 1 to 100.");
    }

    private static void RemovePiece(Resource resource)
    {
        // Fixture removal is immediate and must not invoke the runtime deferred-destruction listener.
        Slot.EmptySlotByResource(resource);
        Object.DestroyImmediate(resource.gameObject);
    }

    private static void ClearSelections()
    {
        SelectionManager.Instance.OnTurnEnd();
        SelectionManager.Instance.UnhighlightAllPlayerBoards();
        foreach (Player player in PlayerManager.Instance.Players)
        {
            player.selectedResources.Clear();
            player.selectedSlots.Clear();
            player.selectedVirtue.Clear();
            player.selectedPlayers.Clear();
            player.selectedPlayer = null;
        }
    }

    private static void Refresh()
    {
        ClearSelections();
        if (PowerManager.Instance != null)
            PowerManager.Instance.RefreshPlaytestState();
        foreach (PlayerInfoCard card in Object.FindObjectsByType<PlayerInfoCard>(FindObjectsSortMode.None))
            card.GetPlayerVirtueCount();
        foreach (PlayerState state in Object.FindObjectsByType<PlayerState>(FindObjectsSortMode.None))
            state.GetPlayerVirtueCount();
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
}
