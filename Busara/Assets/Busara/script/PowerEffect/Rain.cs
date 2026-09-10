using UnityEngine;

[CreateAssetMenu(menuName = "Power/Rain")]
public class Rain : Power
{
    public override void Execute(PowerUse use)
    {
        PowerEffects.Rain(use);
    }
}
