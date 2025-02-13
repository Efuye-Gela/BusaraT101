using System.Collections.Generic;
using UnityEngine;

public class PoliticsDisaster : DisasterEffect
{
    public override void Execute()
    {
        var (Accepted, players) = IsValid(PlayerManager.Instance.Players);
        if (Accepted)
        {
            List<Player> tempPlayers = PlayerManager.Instance.Players;
            tempPlayers.Reverse();

            foreach (Player player in players)
            {
                player.Board.player = tempPlayers[player.Board.boardId];
            }
        }

    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        if (allPlayers != null)
            return (true, PlayerManager.Instance.Players);
        else
            return (false, PlayerManager.Instance.Players);
    }
}
