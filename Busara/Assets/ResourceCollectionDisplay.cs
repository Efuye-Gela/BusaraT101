
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class ResourceCollectionDisplay : MonoBehaviour, SelectionManager.SlotSelectionListener, TurnManager.TurnBeginListener
{
    [SerializeField] SetupCard setupCard = null;
    [SerializeField] List<ResourceType> collectionResources = null;
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
       
    }

    public void Display()
    {
        CollectionDisplay.SetActive(true);
        DisplayCount();
    }

    public void Hide()
    { 
        CollectionDisplay.SetActive(false);
        setupCard = null;
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

    private bool CanBePlaced(Slot slot)
    {
        
        List<Slot> playerSlots = TurnManager.Instance.ActivePlayer.Board.Slots;
        if (!playerSlots.Contains(slot))
        {
            return false;
        }
        List<Slot> neighbouringSlots = BoardManager.GetAdjacentSlots(slot);
        List<Slot> pnSlots = playerSlots.Intersect(neighbouringSlots).ToList();
        if(pnSlots.Count == 0)
        {
            return false;
        }
        foreach (Slot pnslot in pnSlots)
        {
            if (pnslot.isOccupied)
                return false;
        }
        return true;
    }

    private void PlaceResource(Slot slot)
    {
        if (collectionResources.Count > 0)
        {
            if (collectionResources.Contains(selectedResourceType.resourceType))
            {
                if (CanBePlaced(slot))
                { 
                    GameObject spawnedObject = BoardManager.Instance.SpawnByResourceType(selectedResourceType.resourceType, slot.gameObject);
                    tobePlacedResource = spawnedObject.GetComponent<Resource>();
                    Board.PlaceResource(tobePlacedResource, destinationSlot);
                    collectionResources.Remove(tobePlacedResource.resourceType);
                    DisplayCount();
                }
            }  
        }
         if (collectionResources.Count==0)
        {
            Hide();
            TurnManager.Instance.ActivePlayer.hasFinishedSettingUp = true;
            CheckForCompletion();
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        
    }

    private void CheckForCompletion()
    {

        if (PlayerManager.Instance.Players.Where(p => p.hasFinishedSettingUp == true).Count() == PlayerManager.Instance.Players.Count)
        {
            Hide();
            SelectionManager.Instance.RemoveSlotSelectionListener(this);
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
        if (TurnManager.Instance.ActivePlayer.setupcard != null & TurnManager.Instance.ActivePlayer.hasFinishedSettingUp == false)
        {
            setupCard = TurnManager.Instance.ActivePlayer.setupcard;
            collectionResources = new List<ResourceType>(setupCard.collectionResources);
            Display();
        }
        else
            Hide();
    }
}


