using System.Collections.Generic;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect
{
    public override void Execute()
    {
       
        string info = "";
        var (Answer, Players) = IsValid(PlayerManager.Instance.Players);
        if (Answer)
        {
            Debug.Log("Winter has struck!!!");
            PlayerStatDisplay.Instance.Communication("Winter has struck!!!");
            foreach (Player player in Players)
            {
                for (int i = 0; i < player.Virtues.Count; i++)
                {
                    if (i < player.Virtues.Count - 1)
                    {
                        info += player.Virtues[i] + " ,";
                    }
                    else
                        info += player.Virtues[i];
                }

                if (player.Virtues.Count > 0)
                {
                    player.Virtues.Remove(player.Virtues[0]);
                    Debug.Log("After winter");
                    Debug.Log(info);
                }
            }

            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
            PlayerStatDisplay.Instance.Communication("Broke bitches !!!");
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
