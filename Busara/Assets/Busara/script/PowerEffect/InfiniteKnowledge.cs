using UnityEngine;

[CreateAssetMenu(menuName = "Power/InfiniteKnowledge")]
public class InfiniteKnowledge : Power
{

    public override void Execute(PowerUse use)
    {
        use.Caster.knownKingdoms.Add(use.Target.Kingdom);
        string goals = string.Join(", ", System.Array.ConvertAll(use.Target.Kingdom.virtuesForWin,
            goal => $"{goal.NumberofVirtues} {goal.virtues.type}"));
        PowerManager.Instance.Notice($"Private information for {use.Caster.Name}\n" +
            $"{use.Target.Name}: {use.Target.Kingdom.kingdomName}\n{use.Target.Kingdom.kingdomStory}\n" +
            $"{use.Target.Kingdom.power.powerName}\n{use.Target.Kingdom.power.powerDescription}\nGoal: {goals}",
            use.Complete, new PowerDecisionContext(PowerDecisionKind.Notice, use.Caster, use)
            {
                Target = use.Target, Trigger = use.Trigger
            });
    }
}
