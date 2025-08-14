using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/InfiniteKnowledge")]
public class InfiniteKnowledge : Power
{
    public InfiniteKnowledge(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }

    public override void Execute()
    {
            Player TemPlayer = TurnManager.Instance.ActivePlayer;
            Debug.Log($"show me who you are {TemPlayer.selectedPlayer.Kingdom.kingdomName} kingdom." +
            $" lets see what you can use {TemPlayer.selectedPlayer.Kingdom.power.powerName}.");
    }
}
