using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/BlessingOfPlenty")]
public class BlessingOfPlenty : Power, SelectionManager.ResourceSelectionListener, SelectionManager.SlotSelectionListener
{
    [SerializeField] Transform parentTransform;
    private List<Resource> tobeAddedResources = new List<Resource>();
    [Space]
    [SerializeField] private GameObject AirPrefab;
    [SerializeField] private GameObject WaterPrefab;
    [SerializeField] private GameObject FirePrefab;
    [SerializeField] private GameObject EarthPrefab;
    int currentIndex = 0;
    private Resource tobePlacedResource = null;
    private Slot targetSlot = null;

    public BlessingOfPlenty(string powerName, string powerDescription) : base(powerName, powerDescription)
    {
            
    }


    public override void Execute()
    {

        if (IsValid())
        {
            List<Slot> occupiedSlots = TurnManager.Instance.ActivePlayer.Board.GetOccupiedSlots();
            foreach (Slot slot in occupiedSlots)
            {
                if (slot.resource != null)
                    tobeAddedResources.Add(slot.resource);
            }
            currentIndex = 0;
            SpawnExtraResource(tobeAddedResources);
        }
        else
        {
            
        }
    }

    private void SpawnExtraResource(List<Resource> tobeAddedResources)
    {
        GameObject prefabObject = null;
        GameObject newResource;
        Resource currentResource = tobeAddedResources[currentIndex];
        switch (currentResource.resourceType)
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
        newResource = Instantiate(prefabObject, parentTransform);
        //newResource.GetComponent<Resource>().
    }

    private void SpawnNextResources()
    { 
        currentIndex++;
        if (currentIndex < tobeAddedResources.Count)
        {
            SpawnExtraResource(tobeAddedResources);
        }
        else {
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    public override bool IsValid()
    {
        Player TemPlayer = TurnManager.Instance.ActivePlayer;

        List<Virtue> virtuesToRemove = new List<Virtue>(TemPlayer.selectedVirtue);
        int count = TemPlayer.Kingdom.power.virtueCost;
        foreach (Virtue virtue in virtuesToRemove)
        {
            if (count > 0)
            {
                TemPlayer.Virtues.Remove(virtue);
                TemPlayer.selectedVirtue.Remove(virtue);
                count--;
            }
        }
        return true;
    }

    public void Onselection(Resource resource)
    {
        if (tobeAddedResources.Contains(resource))
        {
            tobePlacedResource = resource;
        }
    }

    public void OnDeselection(Resource resource)
    {
        tobePlacedResource = null;
    }

    public void Onselection(Slot slot)
    {
        if (TurnManager.Instance.ActivePlayer.Board.Slots.Contains(slot))
        {
            if (!slot.isOccupied)
            { 
                targetSlot = slot;
                Board.PlaceResource(tobePlacedResource, targetSlot);
                tobeAddedResources.Remove(tobePlacedResource);
                SpawnNextResources();
            }
        }
    }

    public void OnDeselection(Slot slot)
    {
        targetSlot = null;
    }
}
