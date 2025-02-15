using System.Collections.Generic;
using UnityEngine;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;

    public bool IsSpecialTurn = false;
    void Start()
    {
        TurnManager.Instance.OnSpecialCardDrawnEvent += HandleSpacaileTurn; 
    }

    private void HandleSpacaileTurn()
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
        if(TurnManager.Instance.ActivePlayer.Kingdom.power != null)
        {
            if(TurnManager.Instance.ActivePlayer == player)
                TurnManager.Instance.ActivePlayer.Kingdom.power.Excute();
        }
        if (TurnManager.Instance.ActivePlayer != player)
        {
            TurnManager.Instance.ActivePlayer.selectedPlayer = player;
            Debug.Log($"{player.Name} touched.");
        }

    }
}
