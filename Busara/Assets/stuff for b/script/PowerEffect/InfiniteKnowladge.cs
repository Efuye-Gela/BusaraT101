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
        //    if(virtue.Count > 1)
        //    {
        //        virtue.RemoveAt(0);
        //    }
             return (true);
            //else 
            //return (false);
        }
        else
            return (false);
    }
    public override void Excute()
    {
        if (IsVaild(TurnManager.Instance.ActivePlayer.Virtues))
        {
            Debug.Log($"Fear the {TurnManager.Instance.ActivePlayer.Kingdom.kingdomName} kingdom." +
            $" we use the {TurnManager.Instance.ActivePlayer.Kingdom.power.powerName}");
        }
        else
        {
            Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
        }
    }
}
