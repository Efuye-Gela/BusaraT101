using System;
using System.Collections.Generic;
using UnityEngine;

public class PowerManager : Manager<PowerManager>
{
    private Power UsedPower;
    public Action OnPowerActivated;
    public Action OnPowerDeactivated;
    public void ActivatePower(Power PlayersPower)
    {
        UsedPower = PlayersPower;
        Debug.Log($"you wish to use your power: {UsedPower.powerName}");
        OnPowerActivated();
       // ActionManager.Instance.SetAction(ActionManager.ActionState.UsedPower);
    }
    public bool IsValid()
    {
        Player TemPlayer = TurnManager.Instance.ActivePlayer;

        List<Virtue> virtuesToRemove = new List<Virtue>(TemPlayer.selectedVirtue);
        int count = TemPlayer.Kingdom.power.virtueCost;
        if(TemPlayer.selectedPlayer == null)
        {
            PowerUIManager.Instance.PowerMassage("Please select a player to use your power.");
            return false;
        }
        if(TemPlayer.selectedVirtue.Count < TemPlayer.Kingdom.power.virtueCost)
        {
            PowerUIManager.Instance.PowerMassage("You do not have enough virtues selected to use this power.");
            return false;
        }
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

    public void UsePower()
    {
        if (IsValid())
        {
            UsedPower?.Execute();
            OnPowerDeactivated();
        }
    }
    public void CancelPower()
    {
        OnPowerDeactivated();
        Debug.Log("Power usage cancelled.");
    }
}
