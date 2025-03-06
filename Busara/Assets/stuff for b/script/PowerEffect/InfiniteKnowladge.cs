using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/InfiniteKnowledge")]
public class InfiniteKnowledge : Power
{
    public InfiniteKnowledge(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }

    public override bool IsValid()
    {
        Player TemPlayer = TurnManager.Instance.ActivePlayer;

        List<Virtue> virtuesToRemove = new List<Virtue>(TemPlayer.selectedVirtue);
        int count = TemPlayer.Kingdom.power.virtueCost;
        foreach (Virtue virtue in virtuesToRemove)
        {
            if (count > 0)
            {
                TemPlayer.Virtues.Remove(virtue);
                TemPlayer.selectedVirtue.Remove(virtue);
                count--;
            }
        }
        return true;
    }
    public override void Execute()
    {

        if (IsValid())
        {
            Player TemPlayer = TurnManager.Instance.ActivePlayer;
            Debug.Log($"show me who you are {TemPlayer.selectedPlayer.Kingdom.kingdomName} kingdom." +
            $" lets see what you can use {TemPlayer.selectedPlayer.Kingdom.power.powerName}.");
        }
    }
}
