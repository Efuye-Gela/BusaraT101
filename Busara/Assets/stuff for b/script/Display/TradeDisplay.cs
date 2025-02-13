using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeDisplay : MonoBehaviour
{
    [SerializeField] private Image resourceIn;
    [SerializeField] private Image resourceOut;
    [SerializeField] private Button acceptButton;

    public void DisplayOffer(Tuple<ResourceType, ResourceType> tradeOffer)
    {
        resourceOut.sprite = TradeDisplayManager.Instance.GetIconByResourceType(tradeOffer.Item1);
        resourceIn.sprite = TradeDisplayManager.Instance.GetIconByResourceType(tradeOffer.Item2);
        if (TradeManager.Instance.CheckForResources())
            acceptButton.interactable = true;
        else
            acceptButton.interactable = false;
    }

    public void OnTapAccept()
    {
        TradeManager.Instance.PlayerAcceptsTradeOffer();
        if (TurnManager.Instance.isSpecialCardDrawn)
            DisplayOffer(TradeManager.Instance._offerTuple);
    }

    public void OnTapReject()
    {
        TradeManager.Instance.PlayerRejectsTradeOffer();
        if (TurnManager.Instance.isSpecialCardDrawn)
            DisplayOffer(TradeManager.Instance._offerTuple);
    }
    
}
