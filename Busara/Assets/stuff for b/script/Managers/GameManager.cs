using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;

    public bool IsSpecialTurn = false;

    public Player currentPlayer;

    private void Awake()
    {
        currentPlayer = TurnManager.Instance.ActivePlayer;
    }
    void Start()
    {
        TurnManager.Instance.OnSpecialCardDrawnEvent += HandleSpecialTurn; 
    }
    private void Update()
    {
        currentPlayer = TurnManager.Instance.ActivePlayer;
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
            Debug.Log("only on specail turn");
        }
    
    }

    public void OnPowerUser(Player player)
    { 
        if (TurnManager.Instance.ActivePlayer != null && player != null)
        {
            if (TurnManager.Instance.ActivePlayer == player)
            {
                if(PowerVerification(player))
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
        if(currentPlayer == null)
        {
            currentPlayer = TurnManager.Instance.ActivePlayer;
        }
    }

    public void LoadScene(int index)
    {
        SceneManager.LoadScene(index);
    }

    public bool PowerVerification(Player player)
    {
       if(player.Virtues.Count >= player.Kingdom.power.virtueCost)
       {
            int count = player.Kingdom.power.virtueCost;
            if (!player.selectedPlayer)
            {
                PlayerStatDisplay.Instance.Communication("please select a player");
                return false;
            }
            if(player.selectedVirtue.Count <= player.Kingdom.power.virtueCost)
            {
                PlayerStatDisplay.Instance.Communication($"please select {player.Kingdom.power.virtueCost} virtues!!");
                return false;
            }
            foreach(Virtue virtue in player.selectedVirtue)
            {
                if(count > 0)
                {
                    player.Virtues.Remove(virtue);
                    player.selectedVirtue.Remove(virtue);
                    count--;
                }   
            }
            return true;
        }
        else
        {
            PlayerStatDisplay.Instance.Communication("You do not have enough virtue!!!");
            player.selectedVirtue.Clear();
            return false;
        }
            
    }
}
