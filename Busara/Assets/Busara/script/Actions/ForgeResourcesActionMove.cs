using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class ForgeResourcesActionMove : MonoBehaviour
{
    public void OnTapForge()
    {
        bool forged = ForgeManager.Instance.Forge();
        if (forged)
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        
    }
}
