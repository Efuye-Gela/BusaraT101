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
        Player TemPlayer = TurnManager.Instance.ActivePlayer;
        Kingdom TempKingdom = TemPlayer.Kingdom;
        TemPlayer.Kingdom = TemPlayer.selectedPlayer.Kingdom;
        TemPlayer.selectedPlayer.Kingdom = TempKingdom;
        TurnManager.Instance.CompleteTurn(TemPlayer);
    }

}
