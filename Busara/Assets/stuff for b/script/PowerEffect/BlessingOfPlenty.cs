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

        if (IsValid(TurnManager.Instance.ActivePlayer.selectedVirtue))
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
