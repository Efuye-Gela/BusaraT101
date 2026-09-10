using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Imagination")]
public class Imagination : Power
{
    public override void Execute(PowerUse use)
    {
        use.Target.Virtues.Remove(use.ChosenVirtue);
        use.Caster.Virtues.Add(use.ChosenVirtue);
        use.Complete();
    }
}
    