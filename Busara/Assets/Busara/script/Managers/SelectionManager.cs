using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SelectionManager : Manager<SelectionManager>, TurnManager.TurnEndListener
{
    #region RESOURCE SELECTION 

    List<ResourceSelectionListener> resourceSelectionListeners = new List<ResourceSelectionListener>();

    public void AddResourceSelectionListener(ResourceSelectionListener listener)
    {
        if (!resourceSelectionListeners.Contains(listener))
            resourceSelectionListeners.Add(listener);
    }

    public void RemoveResourceSelectionListener(ResourceSelectionListener listener)
    {
        resourceSelectionListeners.Remove(listener);
    }

    public void TriggerResourceSelectionListeners(Resource resource)
    {
        foreach (ResourceSelectionListener listener in resourceSelectionListeners.ToArray())
        {
            listener.Onselection(resource);
        }
    }
    public void TriggerResourceDeselectionListeners(Resource resource)
    {
        foreach (ResourceSelectionListener listener in resourceSelectionListeners.ToArray())
        {
            listener.OnDeselection(resource);
        }
    }

    public interface ResourceSelectionListener
    {
        void Onselection(Resource resource);
        void OnDeselection(Resource resource);
    }

    #endregion

    #region SLOT SELECTION 

    List<SlotSelectionListener> slotSelectionListeners = new List<SlotSelectionListener>();

    public void AddSlotSelectionListener(SlotSelectionListener listener)
    {
        if (!slotSelectionListeners.Contains(listener))
            slotSelectionListeners.Add(listener);
    }

    public void RemoveSlotSelectionListener(SlotSelectionListener listener)
    {
        if (slotSelectionListeners.Contains(listener))
            slotSelectionListeners.Remove(listener);
    }

    public void TriggerSlotSelectionListeners(Slot slot)
    {
        foreach (SlotSelectionListener listener in slotSelectionListeners.ToArray())
        {
            listener.Onselection(slot);
        }
    }
    public void TriggerSlotDelectionListeners(Slot slot)
    {
        foreach (SlotSelectionListener listener in slotSelectionListeners.ToArray())
        {
            listener.OnDeselection(slot);
        }
    }

    public interface SlotSelectionListener
    {
        void Onselection(Slot slot);
        void OnDeselection(Slot slot);
    }

    #endregion


    public event Action<Resource> OnPieceSelected;
    public event Action<Slot> OnSlotSelected;
    public event Action OnDeselectAll;

    private Player currentPlayer => TurnManager.Instance.ActivePlayer;
    private List<Resource> _selectedResources => TurnManager.Instance.ActivePlayer.selectedResources;
    private List<Slot> _selectedSlots => TurnManager.Instance.ActivePlayer.selectedSlots;
    private List<Player> _selectedPlayers => TurnManager.Instance.ActivePlayer.selectedPlayers;
    private List<Virtue> _selectedVirtues => TurnManager.Instance.ActivePlayer.selectedVirtue;

    public Action OnSelectedPlayerChanged;
    public Action OnSelectedVirtueChanged;

    private void Start()
    {
        TurnManager.Instance.AddTurnEndListeners(this);
    }

    public void UnhighlightAll()
    { 
        UnhighlightAllResources();
        UnhighlightAllSlots();

    }

    public void UnhighlightAllResources()
    {
        foreach (Slot slot in TurnManager.Instance.ActivePlayer.Board.GetOccupiedSlots())
        {
            Resource resource = slot.resource;
            resource.UnHighlight();
        }
    }
    public void UnhighlightAllSlots()
    {
        foreach (Slot  slot in TurnManager.Instance.ActivePlayer.Board.Slots)
        {
            slot.UnHighlight();
        }
    }

    public void UnhighlightAllResources(Player player)
    {
        foreach (Slot slot in player.Board.GetOccupiedSlots())
        {
            Resource resource = slot.resource;
            resource.UnHighlight();
        }
    }
    public void UnhighlightAllSlots(Player player)
    {
        foreach (Slot slot in player.Board.Slots)
        {
            slot.UnHighlight();
        }
    }

    public void UnhighlightAll(Player player)
    {
        UnhighlightAllResources(player);
        UnhighlightAllSlots(player);

    }

    public void UnhighlightAllPlayerBoards()
    {
        foreach (Player player in PlayerManager.Instance.Players)
        {
            Instance.UnhighlightAll(player);
        }
    }

    public void OnTurnEnd()
    {
        foreach (Resource resource in _selectedResources.ToList())
        {
            if (currentPlayer.selectedResources.Contains(resource))
            {
                TurnManager.Instance.ActivePlayer.selectedResources.Remove(resource);
                TriggerResourceDeselectionListeners(resource);
            }
        }
        foreach (Slot slot in _selectedSlots.ToList())
        {
            if (currentPlayer.selectedSlots.Contains(slot))
            {
                currentPlayer.selectedSlots.Remove(slot);
                TriggerSlotDelectionListeners(slot);
            }
        }
        foreach (Player player in _selectedPlayers.ToList())
        {
            DeselectPlayer(player);
        }
        foreach (Virtue virtue in _selectedVirtues.ToList())
        {
            DeselectVirtue(virtue);
        }
        UnhighlightAllPlayerBoards();
    }

    public void SelectPlayer(Player player)
    {
        if (!currentPlayer.selectedPlayers.Contains(player))
            currentPlayer.selectedPlayers.Add(player);
        OnSelectedPlayerChanged?.Invoke();
    }

    public void DeselectPlayer(Player player)
    {
        if (currentPlayer.selectedPlayers.Contains(player))
            currentPlayer.selectedPlayers.Remove(player);
        OnSelectedPlayerChanged?.Invoke();
    }

    public void SelectVirtue(Virtue virtue)
    {
        if (!currentPlayer.selectedVirtue.Contains(virtue))
            currentPlayer.selectedVirtue.Add(virtue);
        OnSelectedVirtueChanged?.Invoke();
    }

    public void DeselectVirtue(Virtue virtue)
    {
        if (currentPlayer.selectedVirtue.Contains(virtue))
            currentPlayer.selectedVirtue.Remove(virtue);
        OnSelectedVirtueChanged?.Invoke();
    }


    // Observer pattern using interface from below 
    public void ToggleSelect(Resource resource)
    {
        //need a check if the what action is the player taking 
        if (!currentPlayer.selectedResources.Contains(resource))
        {
           if (currentPlayer.selectedResources.Count == 0 && !TurnManager.Instance.ActivePlayer.hasDrawnResource)
            {
                if (!currentPlayer.Board.Slots.Contains(resource.slot))
                {
                    DisplayManager.Instance.DeliverError("First resource you select must be yours");
                    Debug.Log("First resource you select must be yours");
                    return;
                }
            }
            currentPlayer.selectedResources.Add(resource);
            TriggerResourceSelectionListeners(resource);
        }
        else if (currentPlayer.selectedResources.Contains(resource))
        {
            currentPlayer.selectedResources.Remove(resource);
            TriggerResourceDeselectionListeners(resource);
        }
    }

    public void ToggleSelect(Slot slot)
    {

        if (!currentPlayer.selectedSlots.Contains(slot))
        {
            currentPlayer.selectedSlots.Add(slot);
            TriggerSlotSelectionListeners(slot);
        }
        else if (currentPlayer.selectedSlots.Contains(slot))
        {
            currentPlayer.selectedSlots.Remove(slot);
            TriggerSlotDelectionListeners(slot);
        }
    }

}

