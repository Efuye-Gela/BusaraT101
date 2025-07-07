using System.Collections.Generic;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override void Execute()
    {
        var (Answer, Players) = IsValid(PlayerManager.Instance.Players);
        if (Answer)
        {
            DisplayManager.Instance.ErrorMassage("Winter has struck");
            foreach (Player player in Players)
            {
                if (player.Virtues.Count > 0)
                {
                    player.Virtues.Remove(player.Virtues[0]);
                }
            }
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
            DisplayManager.Instance.ErrorMassage("Hard winter struck \n But all of you are broke");
            Debug.Log("Broke");
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }

    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        List<Player> TempPlayers = new List<Player>();
        foreach (Player player in allPlayers)
        {
            if(player.Virtues.Count > 0)
            {
                TempPlayers.Add(player);
            }
        }
        if(TempPlayers.Count > 0)
        {
            return (true, TempPlayers);
        }
        else
            return (false, PlayerManager.Instance.Players);
    }
}
