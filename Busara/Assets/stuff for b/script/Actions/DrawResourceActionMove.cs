using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System;
using Mono.Cecil;

public class DrawResourceActionMove : MonoBehaviour
{
    [SerializeField] Transform parentTransform;
    [SerializeField] Canvas gameCanvas;
    [Space]
    [SerializeField] private GameObject AirPrefab;
    [SerializeField] private GameObject WaterPrefab;
    [SerializeField] private GameObject FirePrefab;
    [SerializeField] private GameObject EarthPrefab;

    [SerializeField] private List<GameObject> placeableObjects;

    private Resource tobePlacedResource;

    public void OnTapDraw()
    {
        GameObject newResource;
        Card drawnCard = DeckManager.Instance.Draw();
        DeckManager.Instance.GoToNext();
        if (drawnCard == default(Card))
            Debug.LogError("Null card");
        else
        {
            DeckManager.Instance.Cards.Remove(drawnCard);
            if (drawnCard.GetType() == typeof(ResourceCard))
            {
                ResourceCard drawnResourceCard = (ResourceCard)drawnCard;
                
                // TODO: Bad coding
                if (drawnResourceCard.Resource == ResourceType.Water)
                {
                    newResource = Instantiate(WaterPrefab, parentTransform);
                    newResource.GetComponent<Draggable>().canvas = gameCanvas;
                    tobePlacedResource = newResource.GetComponent<Resource>();
                }
                else if (drawnResourceCard.Resource == ResourceType.Air)
                {
                    newResource = Instantiate(AirPrefab, parentTransform);
                    newResource.GetComponent<Draggable>().canvas = gameCanvas;
                    tobePlacedResource = newResource.GetComponent<Resource>();
                }
                else if (drawnResourceCard.Resource == ResourceType.Earth)
                {
                    newResource = Instantiate(EarthPrefab, parentTransform);
                    newResource.GetComponent<Draggable>().canvas = gameCanvas;
                    tobePlacedResource = newResource.GetComponent<Resource>();
                }
                else if (drawnResourceCard.Resource == ResourceType.Fire)
                { 
                    newResource = Instantiate(FirePrefab, parentTransform);
                    newResource.GetComponent<Draggable>().canvas = gameCanvas;
                    tobePlacedResource = newResource.GetComponent<Resource>();
                }
                else
                    newResource = null;

            }
            else if (drawnCard.GetType() == typeof(DisasterCard))
            { 
                // Inititate Diaster Sequence

            }
        }

        DropHandler.OnItemPlaced += ResourcePlaced;
        
    }

    private void ResourcePlaced(Resource resource,Slot slot)
    {
        if (resource != null && tobePlacedResource != null)
            if (resource.index == tobePlacedResource.index)
            {
                if (TurnManager.Instance.ActivePlayer == slot.board.player)
                {
                    Debug.Log("Resource Placed");
                    tobePlacedResource = null;
                    updateState(resource, slot);
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                }
                else
                { 
                    resource.gameObject.transform.SetParent(parentTransform);
                    resource.gameObject.transform.localPosition = Vector3.zero;
                }
            }
    }

    private void updateState(Resource resource, Slot slot)
    {
        slot.isOccupied = true;
        slot.resource = resource;
        resource.slot = slot;
    }
}
