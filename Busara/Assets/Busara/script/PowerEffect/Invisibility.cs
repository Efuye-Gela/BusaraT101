using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Invisibility")]
public class Invisibility : Power
{
    public Invisibility(string powerName, string powerDescription) : base(powerName, powerDescription)
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
            TemPlayer.state.gameObject.SetActive(false);
            TemPlayer.selectedVirtue.Clear();
            TurnManager.Instance.CompleteTurn(TemPlayer);
        }
    }
}
