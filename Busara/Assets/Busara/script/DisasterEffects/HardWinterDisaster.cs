using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override List<Player> GetAffectedPlayers()
    {
        // This disaster affects all players who have at least one virtue.
        return PlayerManager.Instance.Players.Where(p => p.Virtues.Count > 0).ToList();
    }

    public override void Execute(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        StartCoroutine(EndDisasterProcess(affectedPlayers, onDisasterComplete));    

    }
    public IEnumerator EndDisasterProcess(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        if (affectedPlayers.Count == 0)
        {
            DisplayManager.Instance.DeliverInstructions("A resource shortage was announced, but no one had anything to lose!");

            onDisasterComplete?.Invoke();
            yield return null;
        }

        if (affectedPlayers.Count == 0)
        {
            DisplayManager.Instance.DeliverMassage("A hard winter strikes, but no one had any virtues to lose.");
            yield return new WaitForSeconds(0.95f);
        }
        else
        {
            DisplayManager.Instance.DeliverMassage("A hard winter strikes! Every player with virtues must discard one!");
            yield return new WaitForSeconds(0.95f);
            foreach (Player player in affectedPlayers)
            {
                // Remove the first virtue from each affected player's list.
                Virtue removedVirtue = player.Virtues[0];
                player.Virtues.RemoveAt(0);
                Debug.Log($"{player.Name} discarded the virtue: {removedVirtue.name}");
            }
        }
        onDisasterComplete?.Invoke();
    }
}
