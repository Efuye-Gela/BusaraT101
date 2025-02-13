using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TradeDisplayManager : Manager<TradeDisplayManager>
{
    [SerializeField] private List<ResourceTypeIcon> resourceTypeIcons;
    [SerializeField] private GameObject tradeSetupDisplay;
    [SerializeField] private GameObject tradeDisplay;
    [SerializeField] private GameObject tradePlayersDisplay;
    [SerializeField] private GameObject ConfrimDisplay;

    private void OnEnable()
    {
        TradeManager.Instance.TradeInitiated += SetupOffer;
        TradeManager.Instance.TradeCreated += OnTradeCreated;
        TradeManager.Instance.TradeOfferCompleted += OnTradeOfferCompleted;
        TradeManager.Instance.TradeCompleted += TurnOffAllDisplays;
        TradeManager.Instance.TradeTobeCompleted += ConfirmResource;
        TradeManager.Instance.TradeFailed += TurnOffAllDisplays;
    }

    private void ConfirmResource()
    {
        ActivateDisplay(ConfrimDisplay);
    }

    private void SetupOffer(Tuple<ResourceType, List<ResourceType>> tuple)
    {
        ActivateDisplay(tradeSetupDisplay);
        tradeSetupDisplay.GetComponent<TradeSetupDisplay>().DisplayOffer(tuple);
    }

    private void OnTradeCreated(Tuple<ResourceType, ResourceType> tuple)
    {
        ActivateDisplay(tradeDisplay);
        tradeDisplay.GetComponent<TradeDisplay>().DisplayOffer(tuple);
    }

    private void OnTradeOfferCompleted()
    {
        ActivateDisplay(tradePlayersDisplay);
        tradePlayersDisplay.GetComponent<TradePlayersDisplay>().DisplayTradeStatus();
    }


    private void ActivateDisplay(GameObject Display)
    {
        TurnOffAllDisplays();
        Display.SetActive(true);
    }

    private void TurnOffAllDisplays()
    {
        ConfrimDisplay.SetActive(false);
        tradeSetupDisplay.SetActive(false);
        tradeDisplay.SetActive(false);
        tradePlayersDisplay.SetActive(false);
    }

    public Sprite GetIconByResourceType(ResourceType resourceType)
    {
        return resourceTypeIcons.Find(x => x.resourceType == resourceType).sprite;
    }

    public void DisplayButtons()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        
    }

}

[System.Serializable]
public struct ResourceTypeIcon
{
    public ResourceType resourceType;
    public Sprite sprite;
}
