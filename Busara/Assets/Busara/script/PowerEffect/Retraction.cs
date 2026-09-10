using UnityEngine;

[CreateAssetMenu(menuName = "Power/Retraction")]
public class Retraction : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.Retract(use);
    }
}
