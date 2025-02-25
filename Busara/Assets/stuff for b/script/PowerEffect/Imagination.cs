using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Imagination")]
public class Imagination : Power
{
    public Imagination(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsValid(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count == virtueCost)
            {
                List<Virtue> tempVirtuecollection = new List<Virtue>(virtue);
                foreach (Virtue virtueToeDestroyed in tempVirtuecollection)
                {
                    if (TurnManager.Instance.ActivePlayer.Virtues.Contains(virtueToeDestroyed))
                    {
                        virtue.Remove(virtueToeDestroyed);
                        TurnManager.Instance.ActivePlayer.Virtues.Remove(virtueToeDestroyed);
                    }
                }
                if (virtue.Count == 0)
                {
                    TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                    return (true);
                }
                else
                {

                    TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                    return false;
                }
            }
            else
            {
                Debug.Log($"You must select only {virtueCost} virtue to use this power");
                TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                return (false);
            }
        }
        else
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
            return (false);
        }
    }
    public override void Execute()
    {
        if (TurnManager.Instance.ActivePlayer.selectedPlayer == null)
            PlayerStatDisplay.Instance.Communication("Please select a player u wish to use ur power on");
        else
        {
            if (IsValid(TurnManager.Instance.ActivePlayer.selectedVirtue))
            {
        
                if (TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues.Count > 0)
                {
                    Debug.Log("Give me your virtue bitch");
                    PlayerStatDisplay.Instance.Communication("Give me your virtue bitch");
                    TurnManager.Instance.ActivePlayer.Virtues.Add(TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues[0]);
                    TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues.Remove(TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues[0]);
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                    TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                }
            }
            else
            {
                TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
                PlayerStatDisplay.Instance.Communication("YOU CAN NOT USE UR POWER JUST YET");
            }
        }
        
    }
}
