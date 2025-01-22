using UnityEngine;
using System.Collections.Generic;
using System;

public class DrawResourceActionMove : MonoBehaviour
{
    [SerializeField] Transform parentTransform;
    [SerializeField] Canvas gameCanvas;
    [Space]
    [SerializeField] private GameObject AirPrefab;
    [SerializeField] private GameObject WaterPrefab;
    [SerializeField] private GameObject FirePrefab;
    [SerializeField] private GameObject EarthPrefab;

    //[SerializeField] private List<GameObject> placeableObjects;

    private Resource tobePlacedResource;

    public void OnTapDraw()
    {
        if (!GameManager.Instance.multidraw)
        {
            if (TurnManager.Instance.ActivePlayer == GameManager.Instance.lastDrawnPlayer)
            {
                Debug.Log("Can't Draw Resource Again");
                return;
            }
        }

        GameObject newResource;
        Card drawnCard = DeckManager.Instance.Draw();
        GameManager.Instance.lastDrawnPlayer = TurnManager.Instance.ActivePlayer;

        DeckManager.Instance.GoToNext();
        if (drawnCard == default(Card))
            Debug.LogError("Null card");
        else
        {
            DeckManager.Instance.Cards.Remove(drawnCard);
            if (drawnCard.GetType() == typeof(ResourceCard))
            {
                ResourceCard drawnResourceCard = (ResourceCard)drawnCard;
                GameObject prefabObject = null;
                switch (drawnResourceCard.Resource)
                {
                    case ResourceType.Water:
                        prefabObject = WaterPrefab;
                        break;
                    case ResourceType.Earth:
                        prefabObject = EarthPrefab;
                        break;
                    case ResourceType.Fire:
                        prefabObject = FirePrefab;
                        break;
                    case ResourceType.Air:
                        prefabObject = AirPrefab;
                        break;
                    default:
                        break;
                }

                newResource = Instantiate(prefabObject, parentTransform);
                newResource.GetComponent<Draggable>().canvas = gameCanvas;
                tobePlacedResource = newResource.GetComponent<Resource>();


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
