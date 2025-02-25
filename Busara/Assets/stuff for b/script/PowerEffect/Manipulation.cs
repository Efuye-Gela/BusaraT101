using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Manipulation")]
public class Manipulation : Power
{
    public Manipulation(string powerName, string powerDescription): base(powerName, powerDescription)
    {

    }
    public override bool IsValid(List<Virtue> virtue)
    {
        return (false);
    }
    public override void Execute()
    {
        Debug.Log($"Fear the {TurnManager.Instance.ActivePlayer.Kingdom.kingdomName} kingdom." +
            $" we use the {TurnManager.Instance.ActivePlayer.Kingdom.power.powerName}");
    }
}
