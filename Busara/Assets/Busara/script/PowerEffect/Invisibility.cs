using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Invisibility")]
public class Invisibility : Power
{
    public override void Execute(PowerUse use)
    {
        PowerEffects.Steal(use);
    }
}
