using System.Collections.Generic;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override void Execute(Player currentPlayer, List<Player> allPlayers)
    {
        if (currentPlayer != null)
        {
            //TODO: currentPlayer.DiscardVirtue();
            Debug.Log($"{currentPlayer.Name} discarded a virtue.");
        }
    }

    public override bool IsValid(Player currentPlayer, List<Player> allPlayers)
    {
        if(currentPlayer!=null 
                                //&& currentPlayer.virtues.count > 0
                                                )
        return true;
        else 
            return false;
    }
}
