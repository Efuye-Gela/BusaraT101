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
        if (IsValid(TurnManager.Instance.ActivePlayer.selectedVirtue))
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
        else
        {
            
        }
        
    }

    public override bool IsValid(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count == virtueCost)
            {
                List<Virtue> tempVirtuecollection = new List<Virtue>(virtue);
                foreach (Virtue virtueToeDestroyed in tempVirtuecollection)
                {
                    if (TurnManager.Instance.ActivePlayer.Virtues.Contains(virtueToeDestroyed))
                    {
                        virtue.Remove(virtueToeDestroyed);
                        TurnManager.Instance.ActivePlayer.Virtues.Remove(virtueToeDestroyed);
                    }
                }
                if (virtue.Count == 0)
                {
                    TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                    return (true);
                }
                else
                {

                    TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                    return false;
                }
            }
            else
            {
                Debug.Log($"You must select only {virtueCost} virtue to use this power");
                TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
                return (false);
            }
        }
        else
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Clear();
            return (false);
        }
    }

    public void Onselection(Resource resource)
    {
        if(tobeMovedResources.Contains(resource))
            tobeMovedResource = resource;
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
