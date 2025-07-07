using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/IdentitySurfing")]
public class IdentitySurfing : Power
{
    public IdentitySurfing(string powerName, string powerDescription) : base(powerName, powerDescription)
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
        Player TemPlayer = TurnManager.Instance.ActivePlayer;
        if (IsValid())
        {
            //DisplayManager.Instance.ErrorMassage("Give your kingdom");
            Kingdom TempKingdom = TemPlayer.Kingdom;
            TemPlayer.Kingdom = TemPlayer.selectedPlayer.Kingdom;
            TemPlayer.selectedPlayer.Kingdom = TempKingdom;
            TurnManager.Instance.CompleteTurn(TemPlayer);
        }
    }

}
