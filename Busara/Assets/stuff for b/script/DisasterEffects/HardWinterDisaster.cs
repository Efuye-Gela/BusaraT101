using System.Collections.Generic;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override void Execute()
    {
        /* if (currentPlayer != null)
         {
             //TODO: currentPlayer.DiscardVirtue();
             Debug.Log($"{currentPlayer.Name} discarded a virtue.");
         }*/
        Debug.Log("Winter has struck!!!");
        string info = "";
        

        Debug.Log(info);
        foreach (Player player in PlayerManager.Instance.Players)
        {
            for (int i = 0; i < player.Virtues.Count; i++)
            {
                if (i < player.Virtues.Count - 1)
                {
                    info += player.Virtues[i] + " ,";
                }
                else
                    info += player.Virtues[i];
            }

   
            if(player.Virtues.Count > 0)
            {
                player.Virtues.Remove(player.Virtues[0]);
                Debug.Log("After winter");
                Debug.Log(info);
            }


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
