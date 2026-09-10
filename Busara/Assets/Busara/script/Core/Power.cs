using UnityEngine;

public enum PowerTiming
{
    OwnTurn,
    TurnStart,
    OtherTurn,
    PowerReaction,
    ThreatReaction
}

public abstract class Power : ScriptableObject
{
    public string powerName;
    [TextArea]
    public string powerDescription;
    public int virtueCost;
    public PowerTiming timing;
    public bool onlyWhileHidden;

    public static bool PowerVerification(Player player)
    {
        return player != null && player.Kingdom != null && player.Kingdom.power != null
            && PowerRules.CanPay(player.Virtues, player.selectedVirtue, player.Kingdom.power.virtueCost);
    }

    public abstract void Execute(PowerUse use);

    // Kept for existing UnityEvents; validation and payment always go through the manager.
    public void Execute()
    {
        PowerManager.Instance.ActivatePower(this);
    }
}
