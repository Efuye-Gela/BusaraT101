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
        Player TemPlayer = TurnManager.Instance.ActivePlayer;

            TemPlayer.state.gameObject.SetActive(false);
            TemPlayer.selectedVirtue.Clear();
            TurnManager.Instance.CompleteTurn(TemPlayer);
    }
}
