using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
    public override void Execute()
    {
        //foreach (var playerObject in allPlayers)
        //{
        //    var player = playerObject.GetComponent<Player>();
        //    if (player != null)
        //    {
        //        player.RotateBoard();
                
        //    }
           
        //}

        //Debug.Log($"{currentPlayer.Name}'s board was rotated.");
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
