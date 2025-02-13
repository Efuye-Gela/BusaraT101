using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BoardManager : Manager<BoardManager>
{
    public List<Board> gameBoards;
    public static List<Slot> slots = new List<Slot>();

    private void Start()
    {
        foreach (var board in gameBoards)
        {
            foreach (var slot in board.Slots)
            {
                slots.Add(slot);
            }

        }
    }
    public static List<Slot> GetAdjacentSlots(Slot currentSlot, int gridSize = 8)
    {
        int index = currentSlot.Index;
        int row = index / gridSize;
        int col = index % gridSize;

        List<int> neighbourIndexes = new List<int>();

        // Calculate adjacent indices with boundary checks
        int leftIndex = (col > 0) ? index - 1 : -1;                 // Ensure it stays in the same row
        int rightIndex = (col < gridSize - 1) ? index + 1 : -1;     // Ensure it stays in the same row
        int upIndex = (row > 0) ? (row - 1) * gridSize + col : -1;  // Move to the row above
        int downIndex = (row < gridSize - 1) ? (row + 1) * gridSize + col : -1; // Move to the row below

        // Add valid neighbors to the list
        if (leftIndex != -1) neighbourIndexes.Add(leftIndex);
        if (rightIndex != -1) neighbourIndexes.Add(rightIndex);
        if (upIndex != -1) neighbourIndexes.Add(upIndex);
        if (downIndex != -1) neighbourIndexes.Add(downIndex);

        // Map slots by index for efficient lookup
        Dictionary<int, Slot> slotMap = new Dictionary<int, Slot>();
        foreach (var slot in slots)
        {
            slotMap[slot.Index] = slot;
        }

        // Collect valid Slot objects for neighbors
        List<Slot> neighbors = new List<Slot>();
        foreach (var neighborIndex in neighbourIndexes)
        {
            if (slotMap.ContainsKey(neighborIndex) && slotMap[neighborIndex])
            {
                neighbors.Add(slotMap[neighborIndex]);
            }
        }

        return neighbors;
    }


}
