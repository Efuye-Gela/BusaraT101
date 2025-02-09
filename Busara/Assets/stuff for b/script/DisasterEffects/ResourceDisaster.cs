using System.Collections.Generic;
using UnityEngine;

public class ResourceDisaster : DisasterEffect
{
    public ResourceDisaster()
    {
        threshold = 1;
    }
    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        int resourseCount = 0;
        List<Player> playerList = new List<Player>();
        foreach (Player player in allPlayers)
        {
            resourseCount = player.Board.Slots.FindAll(s => s.isOccupied).Count;
            if (player != null && resourseCount > threshold)
            {
                Debug.Log("You lost a resource boho");
                resourseCount = 0;
                playerList.Add(player);
            }
            else
                resourseCount = 0;
        }

        if (playerList.Count == 0)
            return (false, playerList);
        else
            return (true, playerList);

    }

    public override void Execute()
    {
        var (TheAnswer, players) = IsValid(PlayerManager.Instance.Players);
        if (TheAnswer && players != null)
        {
            Debug.Log($"So {players} sucks to be you!!!");
            TurnManager.Instance.OnSpecialCardDrawn(players);
            //TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
            Debug.Log("Broke Bitch");
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

}
