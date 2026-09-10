using UnityEngine;


[CreateAssetMenu(menuName = "Power/IdentitySurfing")]
public class IdentitySurfing : Power
{
    public override void Execute(PowerUse use)
    {
        Kingdom kingdom = use.Caster.Kingdom;
        bool revealed = use.Caster.kingdomRevealed;
        use.Caster.Kingdom = use.Target.Kingdom;
        use.Caster.kingdomRevealed = use.Target.kingdomRevealed;
        use.Target.Kingdom = kingdom;
        use.Target.kingdomRevealed = revealed;
        use.Complete();
    }

}
