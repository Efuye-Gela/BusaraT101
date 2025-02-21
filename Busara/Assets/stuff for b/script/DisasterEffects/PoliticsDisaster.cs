using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
   [SerializeField] List<Player> tempPlayers;

    private void Start()
    {
        tempPlayers = new List<Player>(PlayerManager.Instance.Players);
        tempPlayers.Add(tempPlayers[0]);
        tempPlayers.Remove(tempPlayers[0]);
    }
    public override void Execute()
    {
        var (Accepted, players) = IsValid(PlayerManager.Instance.Players);
        if (Accepted)
        {
            PlayerStatDisplay.Instance.Communication("Change your land bitch!!!");
            /*
             what do i want poletics to do is not reverse and then move cause that will :

            p1 p2 p3 p4
            B1 B2 B3 B4
            p4 p3 p2 p1

            we do not want this 

            whar we want 
            p1 p2 p3 p4
            B1 B2 B3 B4
            p2 p3 p4 p1 

            tempPlayers = P2,P3,P4,P1
                          
             */

            // this is for the board side 
            foreach (Player player in players)
            {
                player.Board.player = tempPlayers[player.Board.boardId];
            }

            tempPlayers.Clear();
            foreach (Player player in PlayerManager.Instance.Players)
            {
                Player newPlayer = Instantiate(player);
                tempPlayers.Add(newPlayer);
            }
            tempPlayers.Add(tempPlayers[0]);
            tempPlayers.Remove(tempPlayers[0]);

            //this is for the player side 
            foreach (Player player in players)
            {
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
