using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
    public override void Execute()
    {
        /*
          access each player first then set each players board to the next player and soon 
         */
        List<Player> tempPlayers = PlayerManager.Instance.Players;
        tempPlayers.Reverse();

       foreach(Player player in PlayerManager.Instance.Players)
       {
            player.Board.player = tempPlayers[player.Board.boardId];
       }
    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        if (allPlayers != null
                                                //&& currentPlayer.virtues.count > 0
                                                )
            return (true, PlayerManager.Instance.Players);
        else
            return (false, PlayerManager.Instance.Players);
    }
}
