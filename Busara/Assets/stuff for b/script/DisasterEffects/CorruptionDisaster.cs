using System;
using System.Collections.Generic;
using UnityEngine;

public class CorruptionDisaster : DisasterEffect
{
    [SerializeField] private int threshold;
    public override (bool,List<Player>) IsValid(List<Player> allPlayers)
    {
        int resourceCount = 0;
        List<Player> playerList = new List<Player>();
        foreach(Player player in allPlayers)
        {
            resourceCount = player.Board.Slots.FindAll(s => s.isOccupied).Count;
            if (player != null && resourceCount > threshold)
            {
                Debug.Log("those who are greedy shall be punished Greedy man");
                resourceCount = 0;
                playerList.Add(player);
            }
            else
                resourceCount = 0;
        }

        if (playerList.Count == 0)
            return (false, playerList);
        else
            return (true, playerList);

    }

    public override void Execute()
    {
        /*[Fix it make it use the selected resource]*/
        var (TheAnswer, players) = IsValid(PlayerManager.Instance.Players);
        if (TheAnswer && players != null)
        {
            Debug.Log($"So {players} were naughty!!!");
            PlayerStatDisplay.Instance.Communication($"So {players} were naughty!!!");
            TurnManager.Instance.OnSpecialCardDrawn(false,players);
        }
        else
        {
            Debug.Log("I see no one was greedy");
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    
}
