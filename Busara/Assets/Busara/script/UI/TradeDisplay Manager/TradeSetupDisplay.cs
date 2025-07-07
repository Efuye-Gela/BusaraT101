using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TradeSetupDisplay : MonoBehaviour
{
    [SerializeField] private List<Button> buttons;
    
    public void DisplayOffer(Tuple<ResourceType, List<ResourceType>> offer)
    {
        List<ResourceType> resourceList = offer.Item2;
        for (int i = 0; i < buttons.Count; i++)
        {
            if (i < resourceList.Count)
            {
                ResourceType resourceType = resourceList[i];
                Button button = buttons[i];

                // Set the icon for the button
                button.gameObject.GetComponent<Image>().sprite = TradeDisplayManager.Instance.GetIconByResourceType(resourceType);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    button.gameObject.GetComponent<Image>().color = Color.green;
                    TradeManager.Instance._offerTuple = new Tuple<ResourceType, ResourceType>(offer.Item1, resourceType);
                });
                button.interactable = true;
            }
            else
            {
                buttons[i].gameObject.GetComponent<Image>().color = Color.green;
                buttons[i].interactable = false;
            }
        }
    }


    public void OnTapOffer()
    {
        TradeManager.Instance.TradeOfferCreated();
    }

    public struct ResourceTypeIcon
    {
        public ResourceType resourceType;
        public Sprite sprite;
    }
}
