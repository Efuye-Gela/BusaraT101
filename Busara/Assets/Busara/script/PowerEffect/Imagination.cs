using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Imagination")]
public class Imagination : Power
{
    public Imagination(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsValid()
    {
        Player TemPlayer = TurnManager.Instance.ActivePlayer;
        if (TemPlayer.selectedPlayer.Virtues.Count == 0)
        {
           // DisplayManager.Instance.ErrorMassage("player does not contain any virtue");
            return false;
        }
        
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
            Debug.Log($" {TemPlayer.name} has taken {TemPlayer.selectedPlayer.Virtues[0].name} " +
            $"virtue from {TemPlayer.selectedPlayer.name}");

            TemPlayer.Virtues.Add(TemPlayer.selectedPlayer.Virtues[0]);
            TemPlayer.selectedPlayer.Virtues.Remove(TemPlayer.selectedPlayer.Virtues[0]);
            TurnManager.Instance.CompleteTurn(TemPlayer);
        }

    }
}
    