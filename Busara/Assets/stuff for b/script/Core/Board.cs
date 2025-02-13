using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class Board : MonoBehaviour
{
    public Player player;
    public int boardId;
    public List<Slot> Slots;


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
}
