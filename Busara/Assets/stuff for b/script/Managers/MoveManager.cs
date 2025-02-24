using UnityEngine;
using System.Collections.Generic;
using AOT;
using Sirenix.Serialization;
using System;

[Serializable]
public class MoveManager : Manager<MoveManager>
{
    [OdinSerialize]
    private Stack<Move> moveHistory = new Stack<Move>();

    public void ExecuteMove(Move move)
    {
        if (move.Validate())
        {
            move.Execute();
            moveHistory.Push(move);
        }
    }

    public void UndoLastMove()
    {
        if (moveHistory.Count > 0)
        {
            var move = moveHistory.Pop();
            move.Undo();
        }
    }
}
