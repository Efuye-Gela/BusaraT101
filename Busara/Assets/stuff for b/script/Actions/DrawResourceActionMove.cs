using UnityEngine;
using System.Collections.Generic;
using System;

public class DrawResourceActionMove : MonoBehaviour,SelectionManager.ResourceSelectionListener, SelectionManager.SlotSelectionListener
{
    [SerializeField] Transform parentTransform;
    [SerializeField] Canvas gameCanvas;
    [Space]
    [SerializeField] private GameObject AirPrefab;
    [SerializeField] private GameObject WaterPrefab;
    [SerializeField] private GameObject FirePrefab;
    [SerializeField] private GameObject EarthPrefab;

    private Resource tobePlacedResource;
    private Resource drawnResource;
    private Slot destinationSlot;
    

    private void Start()
    {
        SelectionManager.Instance.AddResourceSelectionListener(this);
        SelectionManager.Instance.AddSlotSelectionListener(this);
    }

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
        TurnManager.Instance.ActivePlayer.hasDrawnResource = true;
        GameObject newResource;
        Card drawnCard = DeckManager.Instance.Draw();
        GameManager.Instance.lastDrawnPlayer = TurnManager.Instance.ActivePlayer;

        DeckManager.Instance.GoToNext();
        if (drawnCard == default(Card))
            Debug.LogError("Null card");
        else
        {
            DeckManager.Instance.Cards.Add(drawnCard);
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
                drawnResource = newResource.GetComponent<Resource>();

            }
            else if (drawnCard.GetType() == typeof(DisasterCard))
            {
                
                DisasterCard drawnDisasterCard = (DisasterCard)drawnCard;
                drawnDisasterCard.effect.Execute();
            }
        }   

        //DropHandler.OnItemPlaced += ResourcePlaced;
        
    }

    // for drag and drop
    //private void ResourcePlaced(Resource resource,Slot slot)
    //{
    //    if (resource != null && tobePlacedResource != null)
    //    { 
    //        if (resource.index == tobePlacedResource.index)
    //        {
    //            if (TurnManager.Instance.ActivePlayer == slot.board.player)
    //            {
    //                Debug.Log("Resource Placed");                
    //                resource.gameObject.transform.SetParent(slot.gameObject.transform);
    //                resource.gameObject.transform.localPosition = Vector3.zero;
    //                updateState(resource, slot);
    //                tobePlacedResource = null;
    //                destinationSlot = null;
    //                TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    //            }
    //        }
    //    }
    //}
    private void ResourcePlaced()
    {
        if (tobePlacedResource != null)
        {
            if (TurnManager.Instance.ActivePlayer == destinationSlot.board.player)
            {
                Debug.Log("Resource Placed");
                tobePlacedResource.gameObject.transform.SetParent(destinationSlot.gameObject.transform);
                tobePlacedResource.gameObject.transform.localPosition = Vector3.zero;
                updateState(tobePlacedResource, destinationSlot);
                tobePlacedResource = null;
                destinationSlot = null;
                TurnManager.Instance.ActivePlayer.hasDrawnResource = false;
                TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
            }
            
        }
    }

    private void updateState(Resource resource, Slot slot)
    {
        slot.isOccupied = true;
        slot.resource = resource;
        resource.slot = slot;
    }

    public void Onselection(Resource resource)
    {
        if (resource == drawnResource)
            tobePlacedResource = resource;
        else
            Debug.Log("Can't place this resource");
    }

    public void OnDeselection(Resource resource)
    {
        tobePlacedResource = null;
    }

    public void Onselection(Slot slot)
    {
        if (TurnManager.Instance.ActivePlayer.Board.Slots.Contains(slot))
        {
            destinationSlot = slot;
            ResourcePlaced();
        }
        else
        { 
            Debug.Log("Can't place on other players Board");
            destinationSlot = null;
            TurnManager.Instance.ActivePlayer.selectedSlots.Remove(slot);
        }

    }

    public void OnDeselection(Slot slot)
    {
        destinationSlot = null;
    }

    
}
