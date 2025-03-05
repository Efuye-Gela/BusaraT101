using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Invisibility")]
public class Invisibility : Power
{
    public Invisibility(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
 
    public override void Execute()
    {
        TurnManager.Instance.ActivePlayer.state.gameObject.SetActive(false);
        TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    }
}
