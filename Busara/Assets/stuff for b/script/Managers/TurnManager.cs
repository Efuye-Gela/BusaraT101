using System;
using UnityEngine;
using System.Collections.Generic;

public class TurnManager : Manager<TurnManager>
{
    public Player firstPlayer;

    Player activePlayer;
    public Player ActivePlayer => activePlayer;

    public Action<Player> onTurn;

    

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
}
