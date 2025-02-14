using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Invisibility")]
public class Invisibility : Power
{
    public Invisibility(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
        return false;
    }
    public override void Excute()
    {
        TurnManager.Instance.ActivePlayer.state.gameObject.SetActive(false);
    }
}
