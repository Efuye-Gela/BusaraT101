using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/TransformPower")]//how should I use this 
public class TransformPower : Power
{

    public TransformPower(string powerName, string powerDescription) : base(powerName, powerDescription)
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
        //Implement 
    }
}
