using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;
using static ForgeManager;

public class ForgeManager : Manager<ForgeManager>
{
    public List<Resource> forgedResources = new List<Resource>();
    List<Virtue> forgedVirtues = new List<Virtue>();

    public List<Virtue> AllVirtues;

    public Action selectedResourceCleared;
    private readonly List<IForgeListener> forgeListeners = new();

    public void RegisterForgeListener(IForgeListener listener)
    {
        if (!forgeListeners.Contains(listener))
            forgeListeners.Add(listener);
    }

    public void UnregisterForgeListener(IForgeListener listener)
    {
        if (forgeListeners.Contains(listener))
            forgeListeners.Remove(listener);
    }

    public interface IForgeListener
    {
        void OnForgeCompleted(List<Virtue> forgedVirtues, List<Resource> usedResources);
    }


    public bool Forge()
    {
   

    List<Resource> resourceList = new List<Resource>();
        List<Resource> tobeRemovedResources = new List<Resource>();
        List<Resource> selectedResources = TurnManager.Instance.ActivePlayer.selectedResources;
        if (selectedResources.Count < 2)
        {
            Debug.Log("Not enough resources selected to forge.");
            DisplayManager.Instance.DeliverError("Not enough resources selected to forge.");
            return false;
        }

        if (selectedResources[0].slot.GetPlayer()!= TurnManager.Instance.ActivePlayer)
        {
            Debug.Log("You must start with your resource first");
            DisplayManager.Instance.DeliverError("You must start with your resource first");
            return false;
        }

        forgedResources.Clear();


        foreach (var selectedResource in selectedResources)
        {
            resourceList.Add(selectedResource);
        }
        for (int j = 0; j < resourceList.Count - 1; j++)
        {
            if (resourceList[j].resourceType == resourceList[j + 1].resourceType)
            {
                Debug.Log("You can't have the same item forged");
                DisplayManager.Instance.DeliverError("You can't have the same item forged");
                //selectedResources.Clear();
                return false;
            }
        }

        // Process items using a for loop
        List<Player> receivingPlayers = new List<Player>();
        for (int i = 0; i < resourceList.Count - 1; i++)
        {
            Resource firstResource = resourceList[i];
            Resource secondResource = resourceList[i + 1]; // Get the next item to compare

            int firstResourceIndex = resourceList[i].index;
            int secondResourceIndex = resourceList[i + 1].index;

            if (firstResource.IsAdjacentTo(secondResource))
            {
                Virtue forgedVirtue = CheckForgeCombination(firstResource, secondResource);
                if (forgedVirtue != null)
                {
                    UpdateForgeStatus(firstResource, secondResource, receivingPlayers, forgedVirtue);
                    forgedVirtues.Add(forgedVirtue);
                    if (!tobeRemovedResources.Contains(firstResource))
                        tobeRemovedResources.Add(firstResource);
                    if (!tobeRemovedResources.Contains(secondResource))
                        tobeRemovedResources.Add(secondResource);
                }
                else
                    Debug.Log("Combination does not exist, moving to the next item.");
            }
            else
            { 
                Debug.Log("Non AdjacentResources selected.Forge Failed");
                DisplayManager.Instance.DeliverError("Non Adjacent Resources selected.Forge Failed");
                //selectedResources.Clear();
                return false;
            }

        }
        RemoveForgedResources(tobeRemovedResources);
        TurnManager.Instance.ActivePlayer.selectedResources.Clear();
        selectedResourceCleared?.Invoke();
        foreach (var listener in forgeListeners)
        {
            listener.OnForgeCompleted(new List<Virtue>(forgedVirtues), new List<Resource>(tobeRemovedResources));
        }
        forgedVirtues.Clear();
        return true;
        
    }

    private Virtue CheckForgeCombination(Resource resourceA, Resource resourceB)
    {
        foreach (Virtue virtue in AllVirtues)
        {
            if (resourceA.resourceType == virtue.componentOne)
            {
                if (resourceB.resourceType == virtue.componentTwo)
                    return virtue;
            }
            else if (resourceB.resourceType == virtue.componentOne)
            {
                if (resourceA.resourceType == virtue.componentTwo)
                    return virtue;
            }
        }
        return null;
    }

    private void UpdateForgeStatus(Resource firstResource, Resource secondResource, List<Player> receivingPlayers, Virtue virtue)
    {
        if (receivingPlayers.Count == 0)
        {
            receivingPlayers.Add(firstResource.slot.board.player);
            if(!receivingPlayers.Contains(secondResource.slot.board.player))
                receivingPlayers.Add(secondResource.slot.board.player);

        }
        else
        {
            AddVirtueReceivingPlayers(firstResource.slot.board.player, receivingPlayers);
            AddVirtueReceivingPlayers(secondResource.slot.board.player, receivingPlayers);
        }
        
        GiveForges(receivingPlayers, virtue);

    }

    private void AddVirtueReceivingPlayers(Player player, List<Player> receivingPlayers)
    {
        if (!receivingPlayers.Contains(player))
            receivingPlayers.Add(player);
    }


    private void RemoveForgedResources(List<Resource> resources)
    {
        foreach (Resource resource in resources)
        {
            Slot removerSlot = resource.slot;
            removerSlot.EmptySlot();
        }
    }

    private void GiveForges(List<Player> players, Virtue virtue)
    {
        foreach (var player in players)
        {
            player.Virtues.Add(virtue);
        }
    }
}
