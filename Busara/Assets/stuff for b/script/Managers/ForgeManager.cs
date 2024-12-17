using UnityEngine;
using System.Collections.Generic;

public class ForgeManager : MonoBehaviour
{
    public List<Resource> selectedResources = new List<Resource>();
    public List<Resource> forgedResources = new List<Resource>();

    public List<Virtue> AllVirtues;

    private void Start()
    {

    }

    private void Update()
    {

    }

    private void Forge()
    {
        if (selectedResources.Count < 2)
        {
            Debug.Log("Not enough resources selected to forge.");
            return;
        }

        forgedResources.Clear();

        List<Resource> resourceList = new List<Resource>();

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
                return;
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
                UpdateForgeStatus(firstResource, secondResource, forgedVirtue);
            else
                Debug.Log("Combination does not exist, moving to the next item.");

        }
        selectedResources.Clear();
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

    private void UpdateForgeStatus(Resource firstResource, Resource secondResource, Virtue forgedVirtue)
    {
        //List<Player> playersReceiveingVirtues = new List<Player>();
        //foreach (Player player in PlayerManager.Instance.Players)
        //{ 
        //    if (player.Board.Slots.Contains(firstResource))
        //        playersReceiveingVirtues.Add(player);
        //    else if (player.Board.Slots.Contains(secondResource))
        //        playersReceiveingVirtues.Add(player);
        //}

        //foreach (Player player in playersReceiveingVirtues)
        //{
        //    player.Virtues.add(forgedVirtue);
        //}

    }



}
