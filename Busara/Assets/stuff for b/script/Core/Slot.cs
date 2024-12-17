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

    public static Action OnSlotFilled;
    public static Action OnSlotEmptied;

    private void Start()
    {

    }

    private Resource EmptySlot()
    {
        Resource removedResource = this.resource;
        isOccupied = false;
        this.resource = null;
        OnSlotEmptied?.Invoke();
        return removedResource;
    }

    
}
