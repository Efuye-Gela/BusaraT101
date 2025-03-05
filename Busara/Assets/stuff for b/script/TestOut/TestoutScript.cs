using System.Collections.Generic;
using UnityEngine;

public class TestoutScript : MonoBehaviour
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
}
