using System.Collections.Generic;
using UnityEngine;

public class ResourceDisaster : DisasterEffect
{
    [SerializeField] private int threshold;

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        int resourceCount = 0;
        List<Player> playerList = new List<Player>();
        foreach (Player player in allPlayers)
        {
            resourceCount = player.Board.Slots.FindAll(s => s.isOccupied).Count;
            if (player != null && resourceCount >= threshold)
            {
                Debug.Log("You lost a resource");
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
        var (TheAnswer, players) = IsValid(PlayerManager.Instance.Players);
        DisplayManager.Instance.Communication("Resource disaster struck");
        if (TheAnswer && players != null)
        {
            DisplayManager.Instance.Communication("Please select a resource for you to discard!!!");
            TurnManager.Instance.OnSpecialCardDrawn(false, players);
        }
        else
        {
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

}
