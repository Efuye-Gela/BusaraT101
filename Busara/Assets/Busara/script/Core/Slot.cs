using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Slot : MonoBehaviour, SelectionManager.SlotSelectionListener
{
    public Board board;
    public int Index;
    public bool isOccupied;
    public Resource resource;

    [SerializeField] Image highlight;

    public static Action<Slot> OnSlotFilled;
    public static Action<Slot> OnSlotEmptied;

    private Player currentPlayer => TurnManager.Instance.ActivePlayer;

    private void Start()
    {
        SelectionManager.Instance.AddSlotSelectionListener(this);
    }

    public Resource EmptySlot()
    {
        OnSlotEmptied?.Invoke(this);
        Resource removedResource = this.resource;
        removedResource.slot = null;
        isOccupied = false;
        this.resource = null;
        
        return removedResource;
    }

    public static void EmptySlotByResource(Resource resource)
    {
        Slot occupiedSlot = resource.slot;
        resource.slot = null;
        occupiedSlot.resource = null;
        occupiedSlot.isOccupied = false;
    }

    public static void OccupySlot(Slot slot, Resource resource)
    {
        slot.resource = resource;
        slot.isOccupied = true;
        resource.slot = slot;
    }


    public void Onselection(Slot slot)
    {
        slot.Highlight();  //sometimes some null
    }

    public void OnDeselection(Slot slot)
    {
        slot.UnHighlight();  
    }

    public void Highlight()
    {
        if(!isOccupied)
            this.highlight.gameObject.SetActive(true);
    }

    public void UnHighlight()
    {
        this.highlight.gameObject.SetActive(false);
    }

    public Player GetPlayer()
    {
        return board.player;
    }
}
