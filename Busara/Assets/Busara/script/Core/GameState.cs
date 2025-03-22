using System;
using UnityEngine;

[Serializable]
public class GameState : Manager<GameState>
{
    public Player CurrentPlayer => TurnManager.Instance.ActivePlayer;
    public Board CurrentBoard => TurnManager.Instance.ActivePlayer.Board;

    public bool IsValidMove(Player player)
    {
        return player == CurrentPlayer;
    }
}
