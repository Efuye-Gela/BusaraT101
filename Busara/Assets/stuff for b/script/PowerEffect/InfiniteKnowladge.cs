using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/InfiniteKnowladge")]
public class InfiniteKnowladge : Power
{
    public InfiniteKnowladge(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count > 0)
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
                    virtue.Clear();
                    return (true);
                }
                else
                {
                    return false;
                }
            }
            else
                return (false);
        }
        else
            return (false);
    }   
    public override void Excute()
    {
        if (IsVaild(TurnManager.Instance.ActivePlayer.Virtues))
        {
            if (TurnManager.Instance.ActivePlayer.selectedPlayer == null)
                Debug.Log("Please selecte a player u wish to use ur power on");
            else
            {
                Debug.Log($"show me who you are {TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom.kingdomName} kingdom." +
                $" lets see what you can use {TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom.power.powerName}.");
                TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            }
        }
        else
        {
            Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
        }
    }
}
