using System.Collections.Generic;
using UnityEngine;

public abstract class Power : ScriptableObject
{
    public string powerName;
    [TextArea]
    public string powerDescription;
    public int virtueCost;

    public Power(string powerName, string powerDescription)
    {
        this.powerName = powerName;
        this.powerDescription = powerDescription;
    }
    public static bool PowerVerification(Player player)
    {
        if (player.selectedVirtue.Count >= player.Kingdom.power.virtueCost)
        {
            if (!player.selectedPlayer)
            {
                return false;
            }
            return true;
        }
        else
        {
            player.selectedVirtue.Clear();
            return false;
        }

    }
    public abstract void Execute();


}
