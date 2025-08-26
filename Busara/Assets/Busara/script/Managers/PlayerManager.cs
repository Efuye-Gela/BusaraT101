using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

public class PlayerManager : Manager<PlayerManager>
{
    public List<Player> Players;
    public PlayerInfo playerInfo;
    private void Start()
    {
        for (int i = Players.Count - 1; i >= 0; i--)
        {
            if (i < playerInfo.Players.Count)
            {
                Players[i].Name = playerInfo.Players[i].PlayerName;
                Players[i].Kingdom = playerInfo.Players[i].Kingdom;
                Players[i].Board.gameObject.SetActive(true);
            }
            else
            {
                Players[i].Board.gameObject.SetActive(false);
                Players[i].VirtueCard.gameObject.SetActive(false);
                Destroy(Players[i].gameObject);
                Players.RemoveAt(i);
            }
        }
    }

    public Player GetNextPlayer(Player currentPlayer)
    { 
        int currentPlayerIndex = Players.IndexOf(currentPlayer);
        int nextPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        return Players[nextPlayerIndex];
    }

}
