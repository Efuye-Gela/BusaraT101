using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/TransformPower")]//how should I use this 
public class TransformPower : Power
{

    public TransformPower(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
     /*   if (TurnManager.Instance.ActivePlayer)
        {
            //    if(virtue.Count > 1)
            //    {
            //        virtue.RemoveAt(0);
            //    }
            return (true);
            //else 
            //return (false);
        }*/
        //else // 
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
                Debug.Log("power wa");
            }
        }
        else
        {
            Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
        }
    }
}
