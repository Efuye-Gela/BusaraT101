using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class DrawResourceActionMove : MonoBehaviour,SelectionManager.ResourceSelectionListener, SelectionManager.SlotSelectionListener
{
    [SerializeField] GameObject parent;
    [SerializeField] Canvas gameCanvas;


    private Resource toBePlacedResource;
    private Resource drawnResource;
    public Resource PendingResource => drawnResource;
    private Slot destinationSlot;
    

    private void Start()
    {
        SelectionManager.Instance.AddResourceSelectionListener(this);
        SelectionManager.Instance.AddSlotSelectionListener(this);
    }

    public void OnTapDraw()
    {

        if (!ActionManager.Instance.CanPerformAction())
        {
            DisplayManager.Instance.DeliverError("You have already performed an action this turn.");
            return;
        }
        ActionManager.Instance.SetAction(ActionManager.ActionState.DrewCard);

        TurnManager.Instance.ActivePlayer.hasDrawnResource = true; //okay
        GameObject newResource;
        Card drawnCard = DeckManager.Instance.Draw();

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
                newResource = BoardManager.Instance.SpawnByResourceType(drawnResourceCard.Resource, parent);
                newResource.GetComponent<Selectable>().canvas = gameCanvas;
                drawnResource = newResource.GetComponent<Resource>();

            }
            else if (drawnCard.GetType() == typeof(DisasterCard))
            {
                DisasterCard drawnDisasterCard = (DisasterCard)drawnCard;
                DisasterManager.Instance.TriggerDisaster(drawnDisasterCard.effect);
            }
        }  
        
    }
    private void ResourcePlaced()
    {
        if (toBePlacedResource != null && destinationSlot != null && !destinationSlot.isOccupied)
        {
            if (TurnManager.Instance.ActivePlayer == destinationSlot.board.player)
            {
                Debug.Log("Resource Placed");
                Board.PlaceResource(toBePlacedResource, destinationSlot);
                toBePlacedResource = null;
                drawnResource = null;
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
        {
            toBePlacedResource = resource;
        }

    }

    public void OnDeselection(Resource resource)
    {
        toBePlacedResource = null;
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
            //Debug.Log("Can't place on other players Board");
            destinationSlot = null;
            TurnManager.Instance.ActivePlayer.selectedSlots.Remove(slot);
        }

    }

    public void OnDeselection(Slot slot)
    {
        destinationSlot = null;
    }

    
}
