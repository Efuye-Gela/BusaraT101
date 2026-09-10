using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisasterManager : Manager<DisasterManager>
{
    // Event fired when a disaster begins. Passes the list of players who will be affected.
    public static event Action<List<Player>> OnDisasterStart;

    // Event fired when a disaster has fully resolved.
    public static event Action OnDisasterEnd;

    private DisasterEffect currentDisaster;
    private Player disasterPlayer; // The player who drew the card

    public void TriggerDisaster(DisasterEffect disasterEffect)
    {
        currentDisaster = disasterEffect;
        disasterPlayer = TurnManager.Instance.ActivePlayer;

        Debug.Log($"Disaster Triggered: {currentDisaster.name}");
        disasterPlayer.hasDrawnResource = false;
        if (PowerManager.Instance != null)
            PowerManager.Instance.OfferProtection(disasterPlayer, $"{disasterPlayer.Name} drew a disaster card.",
                EndDisaster, ExecuteDisaster, isAttack: false);
        else
            ExecuteDisaster();
    }

    private void ExecuteDisaster()
    {
        List<Player> affectedPlayers = currentDisaster.GetAffectedPlayers();

        OnDisasterStart?.Invoke(affectedPlayers);

        currentDisaster.Execute(affectedPlayers, EndDisaster);
    }
    private void EndDisaster()
    {
        Debug.Log($"Disaster Ended: {currentDisaster.name}");
        // Fire the end event to signal that the disaster is over
        OnDisasterEnd?.Invoke();
        // Complete the turn for the player who drew the disaster card
        Player player = disasterPlayer;
        currentDisaster = null;
        disasterPlayer = null;
        TurnManager.Instance.CompleteTurn(player);
    }
}
