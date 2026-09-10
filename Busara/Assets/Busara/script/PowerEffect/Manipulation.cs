using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Manipulation")]
public class Manipulation : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.ControlTurn(use.Caster);
        use.Complete();
    }
}
