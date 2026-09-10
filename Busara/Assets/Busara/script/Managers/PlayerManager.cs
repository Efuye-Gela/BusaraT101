using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

public class PlayerManager : Manager<PlayerManager>
{
    public List<Player> Players;
    public KingdomCatalog kingdomCatalog;
    public bool dealKingdomsFromCatalog;
    public bool requirePlayerSetup;
    public bool IsSetupComplete { get; private set; }
    public bool IsAwaitingSetup => requirePlayerSetup && !IsSetupComplete;
    public int SupportedPlayerCount => Players == null ? 0 : Players.Count(player =>
        player != null && player.Board != null && player.setUpCard != null);

    private void Start()
    {
        if (IsAwaitingSetup)
            gameObject.AddComponent<PlayerSetupUI>().Initialize(this);
        else if (dealKingdomsFromCatalog && !IsSetupComplete)
            DealKingdoms();
    }

    public bool TryConfigurePlayers(IReadOnlyList<PlayerSetupEntry> entries, out string error)
    {
        if (IsSetupComplete || (TurnManager.Instance != null &&
            (TurnManager.Instance.TurnsStarted || TurnManager.Instance.ActivePlayer != null)))
        {
            error = "Players cannot be changed after the game has started.";
            return false;
        }
        if (!PlayerSetupRules.Validate(entries, kingdomCatalog, SupportedPlayerCount, out error))
            return false;
        List<Player> seats = Players.Where(player =>
            player != null && player.Board != null && player.setUpCard != null).ToList();
        if (seats.Select(player => player.Board).Distinct().Count() != seats.Count ||
            BoardManager.Instance == null || BoardManager.Instance.gameBoards == null ||
            seats.Any(player => player.Board.Slots == null || player.Board.Slots.Count == 0 ||
                player.Board.Slots.Contains(null) || !BoardManager.Instance.gameBoards.Contains(player.Board)) ||
            TurnManager.Instance == null)
        {
            error = "Each player needs a distinct registered board and a turn manager.";
            return false;
        }
        var participants = seats.Take(entries.Count).ToList();
        for (int i = 0; i < participants.Count; i++)
        {
            Player player = participants[i];
            player.Name = entries[i].Name.Trim();
            player.name = player.Name;
            player.Kingdom = entries[i].Kingdom;
            player.IsBotControlled = entries[i].IsBotControlled;
            player.kingdomRevealed = false;
            player.virtuesHidden = false;
            player.knownKingdoms.Clear();
            if (player.Board.BoardOwnerName != null)
                player.Board.BoardOwnerName.text = player.Name;
        }
        foreach (Player player in Players)
        {
            if (player == null || participants.Contains(player))
                continue;
            if (player.Board != null)
            {
                Transform parent = player.Board.transform.parent;
                if (parent != null && parent.GetComponent<GridLayoutGroup>() != null)
                {
                    // An invisible grid cell prevents the surviving boards from shifting to different slot coordinates.
                    var space = new GameObject("Unused Board Space", typeof(RectTransform));
                    space.transform.SetParent(parent, false);
                    space.transform.SetSiblingIndex(player.Board.transform.GetSiblingIndex());
                }
                TurnManager.Instance.RemoveTurnBeginListener(player.Board);
                TurnManager.Instance.RemoveTurnEndListener(player.Board);
                player.Board.gameObject.SetActive(false);
            }
            player.gameObject.SetActive(false);
        }
        Players = participants;
        // Keep the scene's original slot indices and board geometry for adjacency.
        BoardManager.Instance.gameBoards.RemoveAll(board => !participants.Any(player => player.Board == board));
        BoardManager.slots.Clear();
        foreach (Board board in BoardManager.Instance.gameBoards)
            BoardManager.slots.AddRange(board.Slots);
        foreach (PlayerRepresentation representation in FindObjectsByType<PlayerRepresentation>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool participating = participants.Contains(representation.player);
            if (!participating)
                representation.gameObject.SetActive(false);
            else if (representation.PlayerName != null)
                representation.PlayerName.text = representation.player.Name;
        }
        foreach (PlayerInfoCard card in FindObjectsByType<PlayerInfoCard>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!participants.Contains(card.player))
                card.gameObject.SetActive(false);
            else if (card.PlayerName != null)
                card.PlayerName.text = card.player.Name;
        }
        TurnManager.Instance.firstPlayer = participants[0];
        dealKingdomsFromCatalog = false;
        IsSetupComplete = true;
        foreach (Player player in participants)
            if (player.GetComponent<BotPlayerController>() == null)
                player.gameObject.AddComponent<BotPlayerController>();
        error = string.Empty;
        return true;
    }

    public void DealKingdoms()
    {
        if (IsSetupComplete || IsAwaitingSetup)
        {
            Debug.LogWarning("Kingdoms are selected during player setup and cannot be redealt.");
            return;
        }
        if (kingdomCatalog == null || kingdomCatalog.kingdoms.Count < Players.Count ||
            kingdomCatalog.kingdoms.Exists(kingdom => kingdom == null || kingdom.power == null))
        {
            Debug.LogError("Assign a complete kingdom catalog with at least one kingdom per player.");
            return;
        }
        var available = new List<Kingdom>(kingdomCatalog.kingdoms);
        foreach (Player player in Players)
        {
            int index = Random.Range(0, available.Count);
            player.Kingdom = available[index];
            available.RemoveAt(index);
            player.kingdomRevealed = false;
            player.virtuesHidden = false;
            player.knownKingdoms.Clear();
        }
    }

    public Player GetNextPlayer(Player currentPlayer)
    { 
        int currentPlayerIndex = Players.IndexOf(currentPlayer);
        int nextPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        return Players[nextPlayerIndex];
    }

}
