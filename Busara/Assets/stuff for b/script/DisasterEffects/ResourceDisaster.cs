using System.Collections.Generic;
using UnityEngine;

public class ResourceDisaster : DisasterEffect
{

    public override void Execute()
    {
        //if (currentPlayer != null)
        //{
        //    //TODO: currentPlayer.DiscardResources();
        //    Debug.Log($"{currentPlayer.Name} discarded resource.");
        //}
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
