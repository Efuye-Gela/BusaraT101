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
        //Implement 
    }
}
