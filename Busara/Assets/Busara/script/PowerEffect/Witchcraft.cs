using UnityEngine;

[CreateAssetMenu(menuName = "Power/Witchcraft")]
public class Witchcraft : Power
{
    public override void Execute(PowerUse use)
    {
        PowerEffects.Rearrange(use);
    }
}
