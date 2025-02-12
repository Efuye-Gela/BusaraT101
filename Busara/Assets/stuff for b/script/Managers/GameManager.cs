using System.Collections.Generic;
using UnityEngine;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;

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
                    Debug.Log("Please select one resource only");
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

    public void OnPowerUser()
    {
        TurnManager.Instance.ActivePlayer.Kingdom.power.Excute();
    }

    public void GetPlayer(Player player)
    {
        if (TurnManager.Instance.ActivePlayer != player)
        {
            TurnManager.Instance.ActivePlayer.selectedPlayers.Clear();
            TurnManager.Instance.ActivePlayer.selectedPlayers.Add(player);
        }
       /* if(TurnManager.Instance.ActivePlayer != player)
        {
            if (!TurnManager.Instance.ActivePlayer.selectedPlayers.Contains(player))
            {
                TurnManager.Instance.ActivePlayer.selectedPlayers.Add(player);
                Debug.Log("PLayer added");
            }// if we wanted multiple players to be selected at a time for now tho since i do not need that 
        }*/
        //Debug.Log(player.Name +" , "+ player.name +" , "+ player.Kingdom + " , " + player.Kingdom.power + ". ");//just for test out
    }
}
