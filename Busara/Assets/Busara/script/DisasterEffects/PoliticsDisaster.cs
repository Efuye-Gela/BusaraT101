using DG.Tweening;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class PoliticsDisaster : DisasterEffect
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
        StartCoroutine(EndDisasterProcess(affectedPlayers, onDisasterComplete));
    }
    public IEnumerator EndDisasterProcess(List<Player> affectedPlayers, Action onDisasterComplete)
    {

        DisplayManager.Instance.DeliverInstructions("Political upheaval! All players exchange their lands!");
        yield return new WaitForSeconds(0.95f);

        // 1. Save the state of every player's board using the BoardManager.
        Dictionary<Player, string> originalBoardStates = new Dictionary<Player, string>();
        foreach (Player p in affectedPlayers)
        {
            originalBoardStates[p] = p.Board.GetBoardState(p.Board);
        }

        // 2. Determine the new board assignments (each player gets the board from the player to their "right").
        Dictionary<Player, string> newBoardAssignments = new Dictionary<Player, string>();
        for (int i = 0; i < affectedPlayers.Count; i++)
        {
            Player currentPlayer = affectedPlayers[i];
            // The player to the "right" is the previous player in the list (with wrap-around)
            Player previousPlayer = affectedPlayers[(i - 1 + affectedPlayers.Count) % affectedPlayers.Count];
            newBoardAssignments[currentPlayer] = originalBoardStates[previousPlayer];
        }

        // 3. Load the new states onto each player's board using the BoardManager.
        foreach (Player p in affectedPlayers)
        {
            p.Board.LoadBoardState(p.Board, newBoardAssignments[p]);
        }

        yield return new WaitForSeconds(1f);

        // This disaster resolves instantly, so we call the completion callback right away.
        onDisasterComplete?.Invoke();
    }
}
