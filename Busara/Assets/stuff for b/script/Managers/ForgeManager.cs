using UnityEngine;
using System.Collections.Generic;
using static UnityEditor.Experimental.GraphView.GraphView;
using System.Linq;
using System;

public class ForgeManager : Manager<ForgeManager>
{
    public List<Resource> forgedResources = new List<Resource>();
    List<Player> playersReceiveingVirtues = new List<Player>();
    List<Virtue> forgedVirtues = new List<Virtue>();

    public List<Virtue> AllVirtues;

    public Action selectedResourceCleared;


    public bool Forge()
    {
        List<Resource> resourceList = new List<Resource>();
        List<Resource> tobeRemovedResources = new List<Resource>();
        List<Resource> selectedResources = TurnManager.Instance.ActivePlayer.selectedResources;
        if (selectedResources.Count < 2)
        {
            Debug.Log("Not enough resources selected to forge.");
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
                selectedResources.Clear();
                return false;
            }
        }

        // Process items using a for loop
        for (int i = 0; i < resourceList.Count - 1; i++)
        {
            Resource firstResource = resourceList[i];
            Resource secondResource = resourceList[i + 1]; // Get the next item to compare

            int firstResourceIndex = resourceList[i].index;
            int secondResourceIndex = resourceList[i + 1].index;

            Virtue forgedVirtue = CheckForgeCombination(firstResource, secondResource);
            if (forgedVirtue != null)
            { 
                UpdateForgeStatus(firstResource, secondResource);
                forgedVirtues.Add(forgedVirtue);
                if(!tobeRemovedResources.Contains(firstResource))
                    tobeRemovedResources.Add(firstResource);
                if(!tobeRemovedResources.Contains(secondResource))
                    tobeRemovedResources.Add(secondResource);
            }
                
            else
                Debug.Log("Combination does not exist, moving to the next item.");
        }
        GiveForges(playersReceiveingVirtues, forgedVirtues);
        RemoveForgedResources(tobeRemovedResources);
        TurnManager.Instance.ActivePlayer.selectedResources.Clear();
        selectedResourceCleared?.Invoke();
        //Draggable.selectedResourcesChanged?.Invoke();
        playersReceiveingVirtues.Clear();
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

    private void UpdateForgeStatus(Resource firstResource, Resource secondResource)
    {
        if (!playersReceiveingVirtues.Contains(firstResource.slot.board.player))
            playersReceiveingVirtues.Add(firstResource.slot.board.player);
        if (!playersReceiveingVirtues.Contains(secondResource.slot.board.player))
            playersReceiveingVirtues.Add(secondResource.slot.board.player);


        //foreach (Player player in PlayerManager.Instance.Players)
        //{
        //    if (player.Board.Slots.Contains(firstResource.slot))
        //        playersReceiveingVirtues.Add(player);
        //    else if (player.Board.Slots.Contains(secondResource.slot))
        //        playersReceiveingVirtues.Add(player);
        //}

        //foreach (Player player in playersReceiveingVirtues)
        //{
        //    player.Virtues.Add(forgedVirtue);
        //}

    }

    private void RemoveForgedResources(List<Resource> resources)
    {
        foreach (Resource resource in resources)
        {
            Slot removerSlot = resource.slot;
            removerSlot.EmptySlot();
        }
    }

    private void GiveForges(List<Player> players, List<Virtue> virtues)
    {
        foreach (var player in players)
        {
            foreach (var virtue in virtues)
            {
                player.Virtues.Add(virtue);
            }
        }
    }


}
