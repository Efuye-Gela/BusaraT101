using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager> , TurnManager.TurnBeginListener, TurnManager.TurnEndListener
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
        if (ActivePlayer.Kingdom == null || ActivePlayer.Virtues == null)
        {
            Debug.Log("No Kingdom assigned or Virtues list is empty.");
            return;
        }

        Dictionary<VirtueType, int> playerVirtueCounts = new Dictionary<VirtueType, int>();


        foreach (VirtueType type in System.Enum.GetValues(typeof(VirtueType)))
        {
            playerVirtueCounts[type] = 0;
        }
        foreach (Virtue virtue in ActivePlayer.Virtues)
        {
            if (virtue != null)
            {
                playerVirtueCounts[virtue.type]++;
            }
        }

        foreach (Kingdom.VirtuesForCost requirement in ActivePlayer.Kingdom.virtuesForWin)
        {
            if (requirement == null || requirement.virtues == null)
                continue;

            VirtueType requiredType = requirement.virtues.type;
            int requiredCount = requirement.NumberofVirtues;

            if (!playerVirtueCounts.ContainsKey(requiredType) || playerVirtueCounts[requiredType] < requiredCount)
            {
                return;
            }
        }

        Debug.Log(ActivePlayer.Name + " has met the win conditions!");
        WinScreen.SetActive(true);
    }
    public bool PlayerHasResources()
    {
        if (TurnManager.Instance.ActivePlayer != null && TurnManager.Instance.ActivePlayer.hasFinishedSettingUp)
        {
            return TurnManager.Instance.ActivePlayer.Board.GetOccupiedSlots().Count > 0;
        }
        return true;
    }
    public void OnTurnBegin()
    {
        // At the beginning of a turn, check if the active player has any resources.
        if (!PlayerHasResources())
        {
            consecutiveSkips++;
            // If we have skipped more turns than there are players, the game is stuck in a loop.
            if (consecutiveSkips > PlayerManager.Instance.Players.Count)
            {
                Debug.Log("All players have no resources. The game has ended in a stalemate.");
                // Here you would trigger a "Draw" screen or end the game.
                // For now, we can just stop the game loop by disabling the TurnManager.
                if (TurnManager.Instance != null)
                {
                    TurnManager.Instance.enabled = false;
                }
                if (DrawScreen != null)
                    DrawScreen.SetActive(true);
                return; // Stop processing to prevent the loop from continuing.
            }

            // If they have no resources, immediately complete their turn.
            DisplayManager.Instance.DeliverMassage($"{TurnManager.Instance.ActivePlayer.Name} has no resources and skips their turn!");
            StartCoroutine(SkipTurnAfterDelay(TurnManager.Instance.ActivePlayer));
        }
        else
        {
            // If a player can make a move, reset the skip counter.
            consecutiveSkips = 0;
        }
    }

    private System.Collections.IEnumerator SkipTurnAfterDelay(Player playerToSkip)
    {
        // Wait for a short moment to ensure the player sees the message.
        yield return new WaitForSeconds(1.5f);
        TurnManager.Instance.CompleteTurn(playerToSkip);
    }

    public void OnTurnEnd()
    {
        CheckWinConditions();
    }
}
