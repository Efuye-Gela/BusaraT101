using UnityEngine;

[CreateAssetMenu(menuName = "Power/Abundance")]
public class Abundance : Power
{
    public override void Execute(PowerUse use)
    {
        PowerEffects.AddResources(use.Caster, PlayerManager.Instance.Players.Count, use.Complete, use: use);
    }
}
