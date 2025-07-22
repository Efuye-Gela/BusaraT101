using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class ForgeResourcesActionMove : MonoBehaviour
{
    public void OnTapForge()
    {
        if (!ActionManager.Instance.CanPerformAction())
        {
            DisplayManager.Instance.DeliverError("You have already performed an action this turn.");
            return;
        }
        bool forged = ForgeManager.Instance.Forge();
        if (forged)
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    }
}
