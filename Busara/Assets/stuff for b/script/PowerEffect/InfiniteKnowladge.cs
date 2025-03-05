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
        Debug.Log($"show me who you are {TurnManager.Instance.ActivePlayer.selectedPlayers[0].Kingdom.kingdomName} kingdom." +
        $" lets see what you can use {TurnManager.Instance.ActivePlayer.selectedPlayers[0].Kingdom.power.powerName}.");   
    }
}
