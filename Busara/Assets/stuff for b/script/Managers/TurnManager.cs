using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class TurnManager : Manager<TurnManager>
{
    public Player firstPlayer;

    private Player activePlayer;
    public Player ActivePlayer => activePlayer;

    public Action<Player> onTurn;

    private bool isSpecialCardDrawn = false; // Flag for special card state
    private List<Player> specialActionList; //
    Player lastSeqentialPlayer = null;

    public event Action OnSpecialCardDrawnEvent;

    void Start()
    {
        StartTurns();
    }

    public void StartTurns()
    {
        BeginTurn(firstPlayer);
    }

    void BeginTurn(Player player)
    {
        activePlayer = player;
        Debug.Log("Current Turn : " + player.name);
        onTurn?.Invoke(player);
    }

    public bool HasTurn(Player player)
    {
        return activePlayer == player;
    }

    public void CompleteTurn(Player player)
    {
        if (player != activePlayer)
            return;

        if (isSpecialCardDrawn)
        {
            Player nextSpecialPlayer = null;
            Debug.Log("Special Turn Ended for " + player.name);
            if (specialActionList.Count > 1)
            {
                specialActionList.Remove(player);
                nextSpecialPlayer = specialActionList[0];
                EndTurn(activePlayer);
                BeginTurn(nextSpecialPlayer);
            }
            else if(specialActionList.Count==1)
            {
                specialActionList.Remove(player);
                activePlayer = lastSeqentialPlayer;
                Player nextNormalPlayer = GetNextPlayer();
                Debug.Log("Last Special Turn Ended");
                isSpecialCardDrawn = false;
                EndTurn(activePlayer);
                BeginTurn(nextNormalPlayer);
                
            }
            return;
        }

        Player nextPlayer = GetNextPlayer();
        EndTurn(activePlayer);
        Debug.Log("Turn Ended");
        BeginTurn(nextPlayer);
    }

    Player GetNextPlayer()
    {
        return PlayerManager.Instance.GetNextPlayer(activePlayer);
    }

    void EndTurn(Player player)
    {
        activePlayer = null;
    }

    // Triggered when a special card is drawn
    public void OnSpecialCardDrawn(List<Player> players)
    {
        lastSeqentialPlayer = activePlayer;
        Debug.Log("Special Action to be Performed.");
        isSpecialCardDrawn = true;
        specialActionList = players;
        Player nextPlayer = players[0];
        EndTurn(activePlayer);
        BeginTurn(nextPlayer);
        Debug.Log("Special Turn for "+ nextPlayer.name);
        OnSpecialCardDrawnEvent?.Invoke();
    }


    
}
