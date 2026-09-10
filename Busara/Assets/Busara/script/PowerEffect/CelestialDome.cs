using UnityEngine;

[CreateAssetMenu(menuName = "Power/CelestialDome")]
public class CelestialDome : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.CancelPendingThreat();
        use.Complete();
    }
}
