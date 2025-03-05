using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/TransformPower")]//how should I use this 
public class TransformPower : Power
{

    public TransformPower(string powerName, string powerDescription) : base(powerName, powerDescription)
    {
        
    }
  
    public override void Execute()
    {
       if (TurnManager.Instance.ActivePlayer.selectedPlayers.Count > 0)
         Debug.Log("Please select a player u wish to use ur power on");
    }
}
