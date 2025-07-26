using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class CorruptionDisaster : DisasterEffect
{
    private const int RESOURCE_THRESHOLD = 9;
    private Dictionary<Player, int> discardsRequired = new Dictionary<Player, int>();
    public static Action<Dictionary<Player, int>> OnCorruptionDisaster;

    public override List<Player> GetAffectedPlayers()
    {
        return PlayerManager.Instance.Players
            .Where(p => p.Board.GetOccupiedSlots().Count >= RESOURCE_THRESHOLD)
            .ToList();
    }
    public override void Execute(List<Player> affectedPlayers, Action onDisasterComplete)
    {

        StartCoroutine(EndDisasterProcess(affectedPlayers, onDisasterComplete));

    }
    public IEnumerator EndDisasterProcess(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        if (affectedPlayers.Count == 0)
        {
            DisplayManager.Instance.DeliverInstructions("A wave of anti-corruption sweeps the land, but all rulers were found to be just!");
            yield return new WaitForSeconds(0.95f);
            onDisasterComplete?.Invoke();
            yield return null;
        }

        DisplayManager.Instance.DeliverInstructions("Corruption! Wealthy players must discard half of their resources!");
        yield return new WaitForSeconds(0.95f);
        discardsRequired.Clear();
        foreach (Player p in affectedPlayers)
        {
            int resourceCount = p.Board.GetOccupiedSlots().Count;
            discardsRequired[p] = Mathf.FloorToInt(resourceCount / 2f);
        }
        OnCorruptionDisaster?.Invoke(discardsRequired);
        TurnManager.Instance.OnSpecialTurn(false, affectedPlayers);
    }

}
