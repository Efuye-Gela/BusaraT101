using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Power/IdentitySurfing")]
public class IdentitySurfing : Power
{
    public IdentitySurfing(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
  
    public override void Execute()
    {
        PlayerStatDisplay.Instance.Communication("Give your kingdom");
        Kingdom TempKingdom = TurnManager.Instance.ActivePlayer.Kingdom;
        TurnManager.Instance.ActivePlayer.Kingdom = TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom;
        TurnManager.Instance.ActivePlayer.selectedPlayer.Kingdom = TempKingdom;
        TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    }
}
