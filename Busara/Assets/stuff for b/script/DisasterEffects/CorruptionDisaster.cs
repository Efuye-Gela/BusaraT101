using System.Collections.Generic;
using UnityEngine;

public class CorruptionDisaster : DisasterEffect
{
    [SerializeField] private int threshold;

    public CorruptionDisaster(int threshold)
    {
       this.threshold = threshold;
    }
    public override (bool,List<Player>) IsValid(List<Player> allPlayers)
    {
        int resourseCount = 0;
        List<Player> playerList = new List<Player>();
        foreach(Player player in allPlayers)
        {
            foreach (Slot slot in player.Board.Slots)//This is a bad way to do it because n^2 fix it later if possible 
            {
                if (slot.resource)
                {
                    resourseCount++;
                }
            }
            if (player != null && resourseCount > threshold)
            {
                Debug.Log("Bad Bad Boy you Greedy man");
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
            Debug.Log("So you were naughty!!!");

            foreach(Player MeetPlayer in players)
            {
                foreach(Slot slot in MeetPlayer.Board.Slots)
                {
                    if (slot.resource)
                    { 
                        Destroy(slot.resource.gameObject);
                        slot.EmptySlot();
                        break;
                    }
                }
            }
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
            Debug.Log("I see no one was greedy");
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    
}
