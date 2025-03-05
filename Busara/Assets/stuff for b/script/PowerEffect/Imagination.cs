using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Imagination")]
public class Imagination : Power
{
    public Imagination(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override void Execute()
    {
        if (TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues.Count == 0)
        {
            PlayerStatDisplay.Instance.Communication("player does not contain any virtue");
            return;
        }
        List<Virtue> virtuesToRemove = new List<Virtue>(TurnManager.Instance.ActivePlayer.selectedVirtue);
        int count = TurnManager.Instance.ActivePlayer.Kingdom.power.virtueCost;
        foreach (Virtue virtue in virtuesToRemove)
        {
            if (count > 0)
            {
                TurnManager.Instance.ActivePlayer.Virtues.Remove(virtue);
                TurnManager.Instance.ActivePlayer.selectedVirtue.Remove(virtue);
                count--;
            }
        }
        Debug.Log($"Player {TurnManager.Instance.ActivePlayer.Name} has taken {TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues[0].name} " +
            $"virtue from {TurnManager.Instance.ActivePlayer.selectedPlayer.Name}");
        TurnManager.Instance.ActivePlayer.Virtues.Add(TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues[0]);
        TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues.Remove(TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues[0]);
        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
    }
}
    