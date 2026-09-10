using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TradeActionMove : MonoBehaviour
{
    public void OnTapTrade()
    {
        if (PowerManager.Instance != null && PowerManager.Instance.Controller != null)
        {
            DisplayManager.Instance.DeliverError("Manipulation cannot force another player to trade.");
            return;
        }
        if (!ActionManager.Instance.CanPerformAction()) 
        {
            DisplayManager.Instance.DeliverError("You have already performed an action this turn.");
            return; 
        }
        List<Resource> selectedResources = TurnManager.Instance.ActivePlayer.selectedResources;
        // TODO: Game Mode and Ruleset based trade logic
        if (selectedResources.Count != 1)
            return;
        else
        {
            TradeManager.Instance.tobeTradedOutResources = selectedResources;
            TradeManager.Instance.OfferTrade();
        }

    }
}
