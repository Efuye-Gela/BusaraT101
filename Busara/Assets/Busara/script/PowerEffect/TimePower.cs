using UnityEngine;

[CreateAssetMenu(menuName = "Power/Time")]
public class TimePower : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.ScheduleTurn(use.Caster);
        use.Complete();
    }
}
