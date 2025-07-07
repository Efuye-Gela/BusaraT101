using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class MoveResourceActionMove : MonoBehaviour,SelectionManager.SlotSelectionListener, SelectionManager.ResourceSelectionListener
{
    private Resource tobeMovedResource = null;
    private Slot targetSlot = null;
    private List<Slot> availableSlots = new List<Slot>();

    private void Start()
    {
        SelectionManager.Instance.AddResourceSelectionListener(this);
        SelectionManager.Instance.AddSlotSelectionListener(this);
    }

    private void SlotClicked()
    {
        if (targetSlot != null) {

            List<Slot> adjacentSlots = new List<Slot>();
            if (targetSlot.isOccupied == false)
            {
                if (availableSlots.Contains(targetSlot))
                {
                    Board.MoveResource(tobeMovedResource, targetSlot);
                    UnHighlightAvailableSlots(availableSlots);
                    availableSlots.Clear();
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                        
                }

            }
             
        }
    }

    private void ResourceClicked(Resource clickedOnResource)
    {
        if (TurnManager.Instance.ActivePlayer.Board.Slots.Contains(clickedOnResource.slot))
        {
            List<Slot> adjacentSlots = new List<Slot>();
            if (clickedOnResource != null)
            {
                if (clickedOnResource.slot != null)
                {
                    tobeMovedResource = clickedOnResource;

                    adjacentSlots = BoardManager.GetAdjacentSlots(clickedOnResource.slot);
                    foreach (Slot slot in adjacentSlots)
                    {
                        if (!slot.isOccupied)
                        {
                            availableSlots.Add(slot);
                        }
                    }
                    HighlightAvailableSlots(availableSlots);
                }
            }
        }
    }

    private void HighlightAvailableSlots(List<Slot> slots)
    {
        foreach (Slot slot in slots)
        {
            slot.Highlight();
        }
    }

    private void UnHighlightAvailableSlots(List<Slot> slots)
    {
        foreach (Slot slot in slots)
        {
            slot.UnHighlight();
        }
    }

    public void Onselection(Slot slot)
    {
        targetSlot = slot;
        SlotClicked();
        
    }

    public void OnDeselection(Slot slot)
    {
        targetSlot = null;
    }

    public void Onselection(Resource resource)
    {
        if (!TurnManager.Instance.ActivePlayer.hasDrawnResource) 
        {
            tobeMovedResource = resource;
            ResourceClicked(tobeMovedResource);
        }
        if (TurnManager.Instance.ActivePlayer.selectedResources.Count > 1)
        {
            UnHighlightAvailableSlots(availableSlots);
            availableSlots.Clear();
        }
    }

    public void OnDeselection(Resource resource)
    {
        tobeMovedResource = null;
        UnHighlightAvailableSlots(availableSlots);
        availableSlots.Clear();

    }
}
