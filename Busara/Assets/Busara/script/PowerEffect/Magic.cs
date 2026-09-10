using UnityEngine;

[CreateAssetMenu(menuName = "Power/Magic")]
public class Magic : Power
{
    public override void Execute(PowerUse use)
    {
        PowerManager.Instance.GrantTwoActions();
        use.Complete();
    }
}
