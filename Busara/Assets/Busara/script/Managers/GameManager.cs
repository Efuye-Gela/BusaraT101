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

    public void Start()
    {
        TurnManager.Instance.AddTurnBeginListeners(this);
        TurnManager.Instance.AddTurnBeginListeners(this);
    }

    public void OnTapRemove()
    {
        List<Resource> resources = TurnManager.Instance.ActivePlayer.selectedResources;
        if (resources != null && resources.Count > 0)
        {
            if (resources.Count > 1)
                DisplayManager.Instance.Communication("Please select one resource only");
            else
            {
                resources[0].slot.EmptySlot();
                TurnManager.Instance.ActivePlayer.selectedResources.Clear();
                TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            }
        }
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
    public void CheckResource()
    {
        if (TurnManager.Instance != null)
            ActivePlayer = TurnManager.Instance.ActivePlayer;
        int resource = 0;
        if (ActivePlayer != null)
        {
            foreach (Slot slot in ActivePlayer.Board.Slots)
            {
                if (slot.resource)
                {
                    resource++;
                }
            }
            if (resource == 0)
            {
                Debug.Log("NO resource Turn Skip");
            }
        }
    }
    public void setPlayer()
    {
        if(ActivePlayer == null)
        {
            ActivePlayer = TurnManager.Instance.ActivePlayer;
        }
    }

    public void OnTurnBegin()
    {
        //CheckResource();
    }

    public void OnTurnEnd()
    {
        CheckWinConditions();
    }
}
