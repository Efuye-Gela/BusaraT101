using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
    public override void Execute()
    {
        var (Accepted, players) = IsValid(PlayerManager.Instance.Players);
        if (Accepted)
        {
            PlayerStatDisplay.Instance.Communication("Change your land bitch!!!");
            List<Player> tempPlayers =  new List<Player>(PlayerManager.Instance.Players);
            tempPlayers.Add(tempPlayers[0]);
            tempPlayers.Remove(tempPlayers[0]);
            /*
             what do i want poletics to do is not reverse and then move cause that will :

            p1 p2 p3 p4
            B1 B2 B3 B4
            p4 p3 p2 p1

            we do not want this 

            p1 p2 p3 p4
            B1 B2 B3 B4
            p2 p3 p4 p1 

             */

            foreach (Player player in players)
            {
                player.Board.player = tempPlayers[player.Board.boardId];
                player.Board = tempPlayers[player.Board.boardId].Board;
            }
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
           TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }

    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        if (allPlayers != null)
            return (true, PlayerManager.Instance.Players);
        else
            return (false, PlayerManager.Instance.Players);
    }
}
