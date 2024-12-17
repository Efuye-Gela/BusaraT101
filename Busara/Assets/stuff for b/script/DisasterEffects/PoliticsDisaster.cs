using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
    public override void Execute(Player currentPlayer, List<Player> allPlayers)
    {
        //foreach (var playerObject in allPlayers)
        //{
        //    var player = playerObject.GetComponent<Player>();
        //    if (player != null)
        //    {
        //        player.RotateBoard();
                
        //    }
           
        //}

        Debug.Log($"{currentPlayer.Name}'s board was rotated.");
    }

    public override bool IsValid(Player currentPlayer, List<Player> allPlayers)
    {
        if (currentPlayer != null
            //&& currentPlayer.ResourceCount > threshold
            )
            return true;
        else
            return false;
    }
}
