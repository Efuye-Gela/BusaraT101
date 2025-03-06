using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Manipulation")]
public class Manipulation : Power
{
    public Manipulation(string powerName, string powerDescription): base(powerName, powerDescription)
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
            Debug.Log($"Fear the {TurnManager.Instance.ActivePlayer.Kingdom.kingdomName} kingdom." +
                $" we use the {TurnManager.Instance.ActivePlayer.Kingdom.power.powerName}");
        }
    }
}
