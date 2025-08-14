using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Witchcraft")]
public class Witchcraft : Power, SelectionManager.ResourceSelectionListener, SelectionManager.SlotSelectionListener
{
    List<Resource> tobeMovedResources = new List<Resource>();
    private Resource tobeMovedResource = null;
    private Slot targetSlot = null;

    public Witchcraft(string powerName, string powerDescription) : base(powerName, powerDescription)
    {
            
    }

    public override void Execute()
    {
            SelectionManager.Instance.AddResourceSelectionListener(this);
            SelectionManager.Instance.AddSlotSelectionListener(this);

            List<Slot> occupiedSlots = TurnManager.Instance.ActivePlayer.Board.GetOccupiedSlots();
            foreach (Slot slot in occupiedSlots)
            {
                if (slot.resource != null)
                    tobeMovedResources.Add(slot.resource);
            }
    }

    public void Onselection(Resource resource)
    {
        if (tobeMovedResources.Contains(resource))
        {
            TurnManager.Instance.ActivePlayer.selectedResources.Add(resource);
            tobeMovedResource = resource;
        }
    }

    public void OnDeselection(Resource resource)
    {
        tobeMovedResource = null;
    }

    public void Onselection(Slot slot)
    {
        if (TurnManager.Instance.ActivePlayer.Board.Slots.Contains(slot))
        { 
            if (!slot.isOccupied)
            { 
                targetSlot = slot;
                Board.MoveResource(tobeMovedResource, targetSlot);
                tobeMovedResources.Remove(tobeMovedResource);
                if (tobeMovedResources.Count==0)
                {
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                }
            }
        }
    }

    public void OnDeselection(Slot slot)
    {
        targetSlot = null;
    }
}
