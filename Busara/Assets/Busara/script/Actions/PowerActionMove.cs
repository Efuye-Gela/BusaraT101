using System.Collections.Generic;
using UnityEngine;

public class PowerActionMove : MonoBehaviour
{
    public void OnTapPower()
    {
        //if (!ActionManager.Instance.CanPerformAction())
        //{
        //    DisplayManager.Instance.DeliverError("You have already performed an action this turn.");
        //    return;
        //}
        List<Virtue> PlayerVirtues = TurnManager.Instance.ActivePlayer.Virtues;
        // TODO: Game Mode and Ruleset based trade logic
        if (PlayerVirtues.Count < TurnManager.Instance.ActivePlayer.Kingdom.power.virtueCost)
        {
            Debug.Log($"you can not use your power you do not have enough virtue \n" +
                $" you need {TurnManager.Instance.ActivePlayer.Kingdom.power.virtueCost} virtue");
            return;
        }
        else
        {
            PowerManager.Instance.ActivatePower(TurnManager.Instance.ActivePlayer.Kingdom.power);
        }

    }

}
