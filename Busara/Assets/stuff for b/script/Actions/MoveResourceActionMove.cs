using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class MoveResourceActionMove : MonoBehaviour
{
    private Resource tobeMovedResource = null;
    private List<Slot> availableSlots = new List<Slot>();

    private void Start()
    {
        Draggable.OnDraggableClicked += ResourceClicked;
        DropHandler.OnSlotClicked += SlotClicked;
    }

    private void SlotClicked(GameObject gameObject)
    {
        Slot clickedSlot = gameObject.GetComponent<Slot>();
        if (clickedSlot != null) {
            if (clickedSlot.board.player != TurnManager.Instance.ActivePlayer )
            {
                Debug.Log("Can't move other player Pieces");
                return;
            }

            List<Slot> adjacentSlots = new List<Slot>();
            if (clickedSlot != null)
            {
                if (clickedSlot.isOccupied == false)
                {
                    if (availableSlots.Contains(clickedSlot))
                    {
                        //clickedSlot.isOccupied = true;
                        //clickedSlot.resource = tobeMovedResource;
                        Slot.OccupySlot(clickedSlot, tobeMovedResource);
                        tobeMovedResource.gameObject.transform.SetParent(clickedSlot.gameObject.transform);
                        tobeMovedResource.gameObject.transform.localPosition = Vector3.zero;                       
                        Slot.EmptySlotByResource(tobeMovedResource);
                        TurnManager.Instance.ActivePlayer.selectedResources.Clear();
                        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                        
                    }
                }
            } 
        }
    }

    

    private void ResourceClicked(GameObject gameObject)
    {
        Resource clickedOnResource = gameObject.GetComponent<Resource>();
        List<Slot> adjacentSlots = new List<Slot>();
        if (clickedOnResource != null)
        {
            if (clickedOnResource.slot != null)
            {
                tobeMovedResource = clickedOnResource;
                adjacentSlots = BoardManager.GetAdjacentSlots(clickedOnResource.slot);
                foreach (Slot slot in adjacentSlots) 
                {
                    if (!slot.isOccupied) { 
                        //slot.gameObject.GetComponent<Image>().color = Color.red;
                        availableSlots.Add(slot);
                    }
                }
            }
        }
        
    }
}
