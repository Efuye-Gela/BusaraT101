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
        if (player.Virtues.Count >= player.Kingdom.power.virtueCost)
        {
            if (!player.selectedPlayer)
            {
                PlayerStatDisplay.Instance.Communication("please select a player");
                return false;
            }
            if (player.selectedVirtue.Count < player.Kingdom.power.virtueCost)
            {
                PlayerStatDisplay.Instance.Communication($"please select {player.Kingdom.power.virtueCost} virtues!!");
                return false;
            }
            return true;
        }
        else
        {
            PlayerStatDisplay.Instance.Communication("You do not have enough virtue!!!");
            player.selectedVirtue.Clear();
            return false;
        }

    }
    public abstract void Execute();


}
