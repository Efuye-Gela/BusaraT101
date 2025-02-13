using System.Collections.Generic;
using UnityEngine;

public class CorruptionDisaster : DisasterEffect
{
    [SerializeField] private int threshold;

    public CorruptionDisaster(int threshold)
    {
        this.threshold = threshold;
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

    public override void Execute(Player currentPlayer, List<Player> allPlayers)
    {  
        //player.DiscardResources(threshold);
        Debug.Log($"{currentPlayer.Name} discarded resources due to reaching the threshold of {threshold}.");
        
    }

    
}
