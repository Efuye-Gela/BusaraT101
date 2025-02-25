using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/IdentitySurfing")]
public class IdentitySurfing : Power
{
    public IdentitySurfing(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count == 3)
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
    public override void Excute()
    {
        if (TurnManager.Instance.ActivePlayer.selectedPlayer == null)
            PlayerStatDisplay.Instance.Communication("Please selecte a player u wish to use ur power on");
        else
        {
            if (IsVaild(TurnManager.Instance.ActivePlayer.Virtues))
            {
                PlayerStatDisplay.Instance.Communication("Give your kingdom");
                Kingdom TempKingdom = TurnManager.Instance.ActivePlayer.Kingdom;
                TurnManager.Instance.ActivePlayer.Kingdom = TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom;
                TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom = TempKingdom;
                TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            }
            else
            {
                TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
            }
        }
    }
}
