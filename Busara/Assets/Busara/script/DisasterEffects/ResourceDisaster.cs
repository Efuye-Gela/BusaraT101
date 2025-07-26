using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ResourceDisaster : DisasterEffect
{
    public static event Action OnResourceDisaster;
    public override List<Player> GetAffectedPlayers()
    {
        // This disaster affects all players.
        return PlayerManager.Instance.Players.Where(p => p.Board.GetOccupiedSlots().Count > 0).ToList();
    }

    public override void Execute(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        StartCoroutine(EndDisasterProcess(affectedPlayers, onDisasterComplete));
    }
    public IEnumerator EndDisasterProcess(List<Player> affectedPlayers,Action onDisasterComplete)
    {
        if (affectedPlayers.Count == 0)
        {
            DisplayManager.Instance.DeliverInstructions("A resource shortage was announced, but no one had anything to lose!");
            yield return new WaitForSeconds(0.95f);
            onDisasterComplete?.Invoke();
            yield return null;
        }
        DisplayManager.Instance.DeliverInstructions("Resource shortage! Players must discard one resource!");
        yield return new WaitForSeconds(0.95f);
        TurnManager.Instance.OnSpecialTurn(false, affectedPlayers);
        OnResourceDisaster?.Invoke();
    }
}
