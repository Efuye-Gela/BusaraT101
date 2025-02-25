using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/TransformPower")]//how should I use this 
public class TransformPower : Power
{

    public TransformPower(string powerName, string powerDescription) : base(powerName, powerDescription)
    {
        
    }
    public override bool IsValid(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count == virtueCost)
            {
                List<Virtue> tempVirtuecollection = new List<Virtue>(virtue);
                foreach (Virtue virtueTobeDestroyed in tempVirtuecollection)
                {
                    if (TurnManager.Instance.ActivePlayer.Virtues.Contains(virtueTobeDestroyed))
                    {
                        virtue.Remove(virtueTobeDestroyed);
                        TurnManager.Instance.ActivePlayer.Virtues.Remove(virtueTobeDestroyed);
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
        if (IsValid(TurnManager.Instance.ActivePlayer.Virtues))
        {
            if (TurnManager.Instance.ActivePlayer.selectedPlayers.Count > 0)
                Debug.Log("Please select a player u wish to use ur power on");
            else
            {
                Debug.Log("power wa");
            }
        }
        else
        {
            Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
        }
    }
}
