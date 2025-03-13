
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourceCollectionDisplay : MonoBehaviour, SelectionManager.SlotSelectionListener, TurnManager.TurnBeginListener
{
    [SerializeField] List<ResourceType> collectionResources = new List<ResourceType>();
    [SerializeField] GameObject CollectionDisplay;
    [SerializeField] TMP_Text airResourceCountText;
    [SerializeField] TMP_Text earthResourceCountText;
    [SerializeField] TMP_Text fireResourceCountText;
    [SerializeField] TMP_Text waterResourceCountText;

    private Resource tobePlacedResource;
    public ResourceTypeSet selectedResourceType;
    private Slot destinationSlot;

    private void Start()
    {
        TurnManager.Instance.AddTurnBeginListeners(this);
        SelectionManager.Instance.AddSlotSelectionListener(this);
        //Testing
        //List<ResourceType> TestResources = new List<ResourceType>() {
        //    ResourceType.Air,
        //    ResourceType.Air,
        //    ResourceType.Fire,
        //    ResourceType.Fire,
        //    ResourceType.Fire
        //};
        //collectionResources.AddRange(TestResources);
        
    }

    public void Display()
    {
        CollectionDisplay.SetActive(true);
        DisplayCount();
    }

    public void Hide()
    { 
        CollectionDisplay.SetActive(false);
        collectionResources.Clear();
    }
    

    private void DisplayCount()
    {
        int waterResourcesCount = collectionResources.FindAll(r => r == ResourceType.Water).Count;
        waterResourceCountText.text = waterResourcesCount.ToString();
        int airResourcesCount = collectionResources.FindAll(r => r == ResourceType.Air).Count;
        airResourceCountText.text = airResourcesCount.ToString();
        int fireResourcesCount = collectionResources.FindAll(r => r == ResourceType.Fire).Count;
        fireResourceCountText.text = fireResourcesCount.ToString();
        int earthResourcesCount = collectionResources.FindAll(r => r == ResourceType.Earth).Count;
        earthResourceCountText.text = earthResourcesCount.ToString();
    }

    public void Onselection(Slot slot)
    {
        destinationSlot = slot;
        PlaceResource(slot);
    }

    public void OnDeselection(Slot slot)
    {
        destinationSlot = null;
    }

    private void PlaceResource(Slot slot)
    {
        if (collectionResources.Count > 0)
        {
            if (collectionResources.Contains(selectedResourceType.resourceType))
            {
                GameObject spawnedObject = BoardManager.Instance.SpawnByResourceType(selectedResourceType.resourceType, slot.gameObject);
                tobePlacedResource = spawnedObject.GetComponent<Resource>();
                Board.PlaceResource(tobePlacedResource, destinationSlot);
                collectionResources.Remove(tobePlacedResource.resourceType);
                DisplayCount();
            }  
        }
         if (collectionResources.Count==0)
        {
            Hide();
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    public void SetResourceTypeWater()
    {
        selectedResourceType.resourceType = ResourceType.Water;
    }
    public void SetResourceTypeFire()
    {
        selectedResourceType.resourceType = ResourceType.Fire;
    }
    public void SetResourceTypeAir()
    {
        selectedResourceType.resourceType = ResourceType.Air;
    }
    public void SetResourceTypeEarth()
    {
        selectedResourceType.resourceType = ResourceType.Earth;
    }

    public void OnTurnBegin()
    {
        if (TurnManager.Instance.ActivePlayer.resourceTypeCollection.Count>0)
        {
            collectionResources = TurnManager.Instance.ActivePlayer.resourceTypeCollection;
            Display();
        }
    }
}


