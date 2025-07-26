using System;
using System.Collections.Generic;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override List<Player> GetAffectedPlayers()
    {
        // This disaster affects all players.
        return new List<Player>(PlayerManager.Instance.Players);
    }

    public override void Execute(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        if (affectedPlayers.Count < 2)
        {
            Debug.Log("Not enough players for Politics Disaster.");
            onDisasterComplete?.Invoke();
            return;
        }

        DisplayManager.Instance.DeliverMassage("Virtue is for those who deserve it");
        onDisasterComplete?.Invoke();
    }
}
