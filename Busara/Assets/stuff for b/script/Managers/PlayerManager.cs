using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : Manager<PlayerManager>
{
    public List<Player> Players;

    //TODO: Set player based on Game Start Settings

    public Player GetNextPlayer(Player currentPlayer)
    { 
        int currentPlayerIndex = Players.IndexOf(currentPlayer);
        int nextPlayerIndex = (currentPlayerIndex + 1) % Players.Count;
        return Players[nextPlayerIndex];
    }

}
