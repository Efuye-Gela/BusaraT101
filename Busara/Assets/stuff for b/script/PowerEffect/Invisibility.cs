using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Invisibility")]
public class Invisibility : Power
{
    public Invisibility(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count == 2)
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
            {
                Debug.Log("Please selecte exactly 2 virtue");
                virtue.Clear();
                return (false);
            }
        }
        else
            return (false);
    }
    public override void Excute()
    {
        if (TurnManager.Instance.ActivePlayer.selectedPlayer == null)
            Debug.Log("Please selecte a player u wish to use ur power on");
        else
        {
            if (IsVaild(TurnManager.Instance.ActivePlayer.Virtues))
            {
                TurnManager.Instance.ActivePlayer.state.gameObject.SetActive(false);
            }
            else
            {
                Debug.Log("Broke bitch !!!!");
            }
        }
    }
}
