using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;

    public bool IsSpecialTurn = false;

    public Player ActivePlayer;
    public GameObject WinScreen;

    private void Awake()
    {
        MakeFullScreen();
        ActivePlayer = TurnManager.Instance.ActivePlayer;
    }

    void Start()
    {
       
        if (TurnManager.Instance!=null)
            TurnManager.Instance.OnSpecialCardDrawnEvent += HandleSpecialTurn; 
    }

    private void Update()
    {
        ActivePlayer = TurnManager.Instance.ActivePlayer;
    }
    private void HandleSpecialTurn()
    {
        IsSpecialTurn = true;
    }

    public void OnTapRemove()
    {
        if (IsSpecialTurn)
        {
            List<Resource> resources = TurnManager.Instance.ActivePlayer.selectedResources;
            if (resources != null && resources.Count > 0)
            {
                if (resources.Count > 1)
                    PlayerStatDisplay.Instance.Communication("Please select one resource only");
                else
                {
                    resources[0].slot.EmptySlot();
                    TurnManager.Instance.ActivePlayer.selectedResources.Clear();
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                }
            }
        }
        else
        {
            Debug.Log("only on special turn");
        }
    
    }

    public void OnPowerUser(Player player)
    { 
        if (TurnManager.Instance.ActivePlayer != null && player != null)
        {
            if (TurnManager.Instance.ActivePlayer == player)
            {
                if(Power.PowerVerification(player))
                    TurnManager.Instance.ActivePlayer.Kingdom.power.Execute();
            }
            else if (TurnManager.Instance.ActivePlayer != player)
            {
                TurnManager.Instance.ActivePlayer.selectedPlayer = player;
                Debug.Log($"{player.Name} has been selected.");
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

    public void LoadScene(int index)
    {
        SceneManager.LoadScene(index);
    }

    public void MakeFullScreen()
    {
        Screen.fullScreen = true;
        //Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, true);
    }

    public void CheckWinConditions()
    {
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
                TurnManager.Instance.CompleteTurn(ActivePlayer);
            }
        }
    }
}
