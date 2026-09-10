using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/BlessingOfPlenty")]
public class BlessingOfPlenty : Power
{
    public override void Execute(PowerUse use)
    {
        var copies = PowerRules.Resources(use.Caster).Select(resource => resource.resourceType).ToList();
        PowerEffects.AddResources(use.Caster, copies.Count, use.Complete, copies, use);
    }
}
