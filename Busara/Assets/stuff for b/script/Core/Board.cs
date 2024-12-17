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
}
