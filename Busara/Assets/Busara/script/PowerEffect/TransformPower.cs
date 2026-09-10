using System.Collections.Generic;
using System.Security.Principal;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/TransformPower")]
public class TransformPower : Power
{

    public override void Execute(PowerUse use)
    {
        foreach (Virtue virtue in use.Exchange)
            use.Caster.Virtues.Remove(virtue);
        for (int i = 0; i < use.Exchange.Count; i++)
            use.Caster.Virtues.Add(use.ChosenVirtue);
        use.Complete();
    }
}
