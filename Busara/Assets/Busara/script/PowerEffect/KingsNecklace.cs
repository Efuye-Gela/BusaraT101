using UnityEngine;

[CreateAssetMenu(menuName = "Power/KingsNecklace")]
public class KingsNecklace : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.CancelPendingPower();
        use.Complete();
    }
}
