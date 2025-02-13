using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public class Slot : MonoBehaviour
{
    public Board board;
    public int Index;
    public bool isOccupied;
    public Resource resource;

    public static Action<Slot> OnSlotFilled;
    public static Action<Slot> OnSlotEmptied;

    private void Start()
    {

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

}
