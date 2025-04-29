using System.Collections.Generic;
using UnityEngine;

public class TestOutScript : MonoBehaviour
{
    [SerializeField]
      Virtue SpannableVirtue;

    void Update()
    {

        if (Input.GetKeyDown(KeyCode.V))
        {
            spawnVirtue();
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            spawnForOnePlayer();
        }
        if (Input.GetKeyDown(KeyCode.A))
        {
            spawnForActivePlayer();
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            //TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            int resource = 0;
            if (TurnManager.Instance.ActivePlayer != null)
            {
                foreach (Slot slot in TurnManager.Instance.ActivePlayer.Board.Slots)
                {
                    if (slot.resource)
                    {
                        resource++;
                    }
                }
                if (resource == 0)
                {
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                    Debug.Log("You are not allowed to play");
                }
            }
        }
    }
    public void spawnVirtue()
    {
        foreach(Player player in PlayerManager.Instance.Players)
        {
            player.Virtues.Add(SpannableVirtue);
        }
        Debug.Log("virtue add to all for test out!!!");
    }
    public void spawnForOnePlayer()
    {
        if (TurnManager.Instance.ActivePlayer.selectedPlayer)
            TurnManager.Instance.ActivePlayer.selectedPlayer.Virtues.Add(SpannableVirtue);
        else
            Debug.Log("select a player");
    }

    public void spawnForActivePlayer()
    {
        TurnManager.Instance.ActivePlayer.Virtues.Add(SpannableVirtue);
    }

    public void TOn()
    {
        foreach (VirtueUI VUI in TurnManager.Instance.ActivePlayer.state.VirtueUIList)
        {
            VUI.instance.TurnOnINCDEC();
        }
    }
}
