using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.UI;

[Serializable]
public class Board : MonoBehaviour, TurnManager.TurnBeginListener, TurnManager.TurnEndListener
{
    public Player player;
    public int boardId;
    public List<Slot> Slots;
    public Image Highlight;

    private void Start()
    {
        TurnManager.Instance.AddTurnEndListeners(this);
        TurnManager.Instance.AddTurnBeginListeners(this);
    }
    public Slot GetSlotByIndex(int searchIndex)
    {
        return Slots.FirstOrDefault(sl => sl.Index == searchIndex);
    }

    public List<Slot> GetOccupiedSlots()
    {
        List<Slot> occupiedSlots = new List<Slot>();
        foreach (var slot in Slots)
        {
            if (slot.isOccupied)
            {
                occupiedSlots.Add(slot);
            }
        }
        return occupiedSlots;
    }

    public List<Slot> GetOccupiedSlotByResourceType(ResourceType type)
    {
        List<Slot> slotsOfType = new List<Slot>();
        List<Slot> occupiedSlots = new List<Slot>();

        occupiedSlots = GetOccupiedSlots();
        foreach (var slot in occupiedSlots)
        {
            if (slot.resource != null && slot.resource.resourceType == type)
                slotsOfType.Add(slot);
        }
        return slotsOfType;
    }

    public static void MoveResource(Resource tobeMovedResource, Slot targetSlot)
    {
        Slot.EmptySlotByResource(tobeMovedResource);
        Slot.OccupySlot(targetSlot, tobeMovedResource);
        tobeMovedResource.gameObject.transform.SetParent(targetSlot.gameObject.transform);
        tobeMovedResource.gameObject.transform.localPosition = Vector3.zero;
        
    }

    public static void PlaceResource(Resource tobePlacedResource, Slot destinationSlot)
    {
        tobePlacedResource.gameObject.transform.SetParent(destinationSlot.gameObject.transform);
        tobePlacedResource.gameObject.transform.localPosition = Vector3.zero;
        destinationSlot.isOccupied = true;
        destinationSlot.resource = tobePlacedResource;
        tobePlacedResource.slot = destinationSlot;
        tobePlacedResource = null;
        destinationSlot = null;
    }

    public void HighlightBoard()
    { 
        Highlight.gameObject.SetActive(true);
    }

    public void UnHighlightBoard()
    {
        Highlight.gameObject.SetActive(false);
    }

    public void OnTurnBegin()
    {
        foreach (Player player in PlayerManager.Instance.Players)
        {
            if(TurnManager.Instance.ActivePlayer==player)
                player.Board.HighlightBoard();
            else
                player.Board.UnHighlightBoard();

        }   
        
    }
    public void OnTurnEnd()
    {
        foreach (Player player in PlayerManager.Instance.Players)
        {
            if (TurnManager.Instance.ActivePlayer == player)
                player.Board.HighlightBoard();
            else
                player.Board.UnHighlightBoard();

        }
    }
}
