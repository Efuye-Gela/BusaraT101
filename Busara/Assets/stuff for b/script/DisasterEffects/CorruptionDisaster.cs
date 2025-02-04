using System.Collections.Generic;
using UnityEngine;

public class CorruptionDisaster : DisasterEffect
{
    [SerializeField] private int threshold;

    public CorruptionDisaster(int threshold)
    {
       // this.threshold = threshold;
    }
    public override bool IsValid(Player currentPlayer, List<Player> allPlayers)
    {
        int resourseCount = 0;
        foreach(Slot slot in currentPlayer.Board.Slots)
        {
            if (slot.resource)
            {
                resourseCount++;
            }
        }
        if (currentPlayer != null &&  resourseCount > threshold)
        {
            Debug.Log("Bad Bad Boy you Greedy man");
            resourseCount = 0;
            return true;
        }

        else
            return false;
    }

    public override void Execute()
    {
      foreach(Player player in PlayerManager.Instance.Players)
        {
            if (IsValid(player, PlayerManager.Instance.Players))
            {
                Debug.Log($"{player.name} discarded resources due to reaching the threshold of {threshold}.");
                foreach (Slot slot in player.Board.Slots)
                {
                    if (slot.resource)
                    {
                        Destroy(slot.resource.gameObject); 
                        slot.EmptySlot();
                    }
                }
            }
        }

    }

    
}
