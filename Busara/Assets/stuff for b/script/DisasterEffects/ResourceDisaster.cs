using System.Collections.Generic;
using UnityEngine;

public class ResourceDisaster : DisasterEffect
{

    public override void Execute(Player currentPlayer, List<Player> allPlayers)
    {
        if (currentPlayer != null)
        {
            //TODO: currentPlayer.DiscardResources();
            Debug.Log($"{currentPlayer.Name} discarded resource.");
        }
    }

    public override bool IsValid(Player currentPlayer, List<Player> allPlayers)
    {
        if (currentPlayer != null)
            return true;
        else
            return false;
    }
}
