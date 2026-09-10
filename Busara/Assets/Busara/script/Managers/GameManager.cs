using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager>, TurnManager.TurnBeginListener, TurnManager.TurnEndListener
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;

    public Player ActivePlayer;
    public GameObject WinScreen;
    public GameObject DrawScreen;

    private int consecutiveSkips = 0;

    public void Start()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnBeginListeners(this);
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnEndListeners(this);
    }

    public void CheckWinConditions()
    {
        if (TurnManager.Instance != null)
            ActivePlayer = TurnManager.Instance.ActivePlayer;
        var candidates = new List<Player>(PlayerManager.Instance.Players);
        candidates.Remove(ActivePlayer);
        candidates.Insert(0, ActivePlayer);
        foreach (Player player in candidates)
        {
            if (!HasWon(player))
                continue;
            ActivePlayer = player;
            Debug.Log(player.Name + " has met the win conditions!");
            WinScreen.SetActive(true);
            return;
        }
    }

    private bool HasWon(Player player)
    {
        if (player == null || player.Kingdom == null || player.Virtues == null ||
            player.Kingdom.virtuesForWin == null || player.Kingdom.virtuesForWin.Length == 0)
            return false;

        Dictionary<VirtueType, int> playerVirtueCounts = new Dictionary<VirtueType, int>();


        foreach (VirtueType type in System.Enum.GetValues(typeof(VirtueType)))
        {
            playerVirtueCounts[type] = 0;
        }
        foreach (Virtue virtue in player.Virtues)
        {
            if (virtue != null)
            {
                playerVirtueCounts[virtue.type]++;
            }
        }

        foreach (Kingdom.VirtuesForCost requirement in player.Kingdom.virtuesForWin)
        {
            if (requirement == null || requirement.virtues == null)
                return false;

            VirtueType requiredType = requirement.virtues.type;
            int requiredCount = requirement.NumberofVirtues;

            if (!playerVirtueCounts.ContainsKey(requiredType) || playerVirtueCounts[requiredType] < requiredCount)
            {
                return false;
            }
        }

        return true;
    }
    public bool PlayerHasResources()
    {
        if (TurnManager.Instance.isSpecialCardDrawn)
            return true;
        if (TurnManager.Instance.ActivePlayer != null && TurnManager.Instance.ActivePlayer.hasFinishedSettingUp)
        {
            return TurnManager.Instance.ActivePlayer.Board.GetOccupiedSlots().Count > 0;
        }
        return true;
    }
    public void CheckIfPlayerHasResource()
    {
        if (!PlayerHasResources())
        {
            consecutiveSkips++;
            if (consecutiveSkips > PlayerManager.Instance.Players.Count)
            {
                Debug.Log("All players have no resources. The game has ended in a stalemate.");
                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.enabled = false;
                }
                if (DrawScreen != null)
                    DrawScreen.SetActive(true);
                return;
            }
            DisplayManager.Instance.DeliverInstructions($"{TurnManager.Instance.ActivePlayer.Name} has no resources turn Skipped!");
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            //StartCoroutine(SkipTurnAfterDelay(TurnManager.Instance.ActivePlayer));
        }
        else
        {
            consecutiveSkips = 0;
        }
    }
    private IEnumerator SkipTurnAfterDelay(Player playerToSkip)
    {
        yield return new WaitForSeconds(1.5f);
        TurnManager.Instance.CompleteTurn(playerToSkip);
    }
    public void OnTurnBegin()
    {
        CheckIfPlayerHasResource();
    }

    public void OnTurnEnd()
    {
        CheckWinConditions();
    }
}