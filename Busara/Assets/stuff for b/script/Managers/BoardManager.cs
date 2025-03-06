using NUnit.Framework;
using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class BoardManager : Manager<BoardManager>
{
    public List<Board> gameBoards;
    public static List<Slot> slots = new List<Slot>();
    [Space]
    [SerializeField] private GameObject AirPrefab;
    [SerializeField] private GameObject WaterPrefab;
    [SerializeField] private GameObject FirePrefab;
    [SerializeField] private GameObject EarthPrefab;

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

   
    public void SaveGameState()
    {
        System.Text.StringBuilder state = new System.Text.StringBuilder();

        foreach (var board in gameBoards)
        {
            foreach (var slot in board.Slots)
            {
                if (!slot.isOccupied)
                {
                    state.Append('0');
                }
                else
                {
                    switch (slot.resource.resourceType)
                    {
                        case ResourceType.Fire:
                            state.Append('1');
                            break;
                        case ResourceType.Air:
                            state.Append('2');
                            break;
                        case ResourceType.Water:
                            state.Append('3');
                            break;
                        case ResourceType.Earth:
                            state.Append('4');
                            break;
                    }
                }
            }
        }

        PlayerPrefs.SetString("BoardState", state.ToString());
        PlayerPrefs.Save();
        Debug.Log("Game state saved: " + state.ToString());
    }

    public void LoadGameState()
    {
        if (!PlayerPrefs.HasKey("BoardState"))
        {
            Debug.Log("No saved game state found!");
            return;
        }

        string state = PlayerPrefs.GetString("BoardState");
        if (state.Length != 64)  // Validate state length
        {
            Debug.LogError("Invalid save state length!");
            return;
        }

        // Clear existing resources
        foreach (var board in gameBoards)
        {
            foreach (var slot in board.Slots)
            {
                if (slot.resource != null)
                {
                    Destroy(slot.resource.gameObject);
                    slot.resource = null;
                    slot.isOccupied = false;
                }
            }
        }

        // Load saved state
        int stateIndex = 0;
        foreach (var board in gameBoards)
        {
            foreach (var slot in board.Slots)
            {
                char resourceChar = state[stateIndex++];
                if (resourceChar != '0')
                {
                    ResourceType type = ResourceType.Fire; // Default initialization
                    switch (resourceChar)
                    {
                        case '1': type = ResourceType.Fire; break;
                        case '2': type = ResourceType.Air; break;
                        case '3': type = ResourceType.Water; break;
                        case '4': type = ResourceType.Earth; break;
                    }
                    GameObject prefabObject = null;
                    switch (type)
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

                    //newResource = Instantiate(prefabObject, parentTransform);
                    //GameObject resourcePrefab = GetResourcePrefab(type);
                    if (prefabObject != null)
                    {
                        GameObject resourceObj = Instantiate(prefabObject, slot.transform);
                        resourceObj.transform.localPosition = Vector3.zero;
                        Resource resource = resourceObj.GetComponent<Resource>();
                        Board.PlaceResource(resource, slot);
                    }
                }
            }
        }
        Debug.Log("Game state loaded!");
    }
    
}
