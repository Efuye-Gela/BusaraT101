using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class ResourceCollectionDisplay : MonoBehaviour, SelectionManager.SlotSelectionListener, SelectionManager.ResourceSelectionListener, TurnManager.TurnBeginListener
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

    private Resource resourceToMove = null;
    private bool isMovingResource = false;
    private List<Slot> availableMoveSlots = new List<Slot>();

    private void Start()
    {
        TurnManager.Instance.AddTurnBeginListeners(this);
        SelectionManager.Instance.AddSlotSelectionListener(this);
        SelectionManager.Instance.AddResourceSelectionListener(this);
    }

    public void Display()
    {
        CollectionDisplay.SetActive(true);
        DisplayCount();
        ActionManager.Instance.SetAction(ActionManager.ActionState.ResourceSetup);
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
        if (TurnManager.Instance.ActivePlayer.setUpCard == null || TurnManager.Instance.ActivePlayer.hasFinishedSettingUp == true)
            return;

        if (isMovingResource)
        {
            if (availableMoveSlots.Contains(slot))
            {
                Board.MoveResource(resourceToMove, slot);
                ResetMoveState();
            }
            else
            {
                DisplayManager.Instance.DeliverError("You cannot move a resource to an adjacent slot on your board.");
            }
        }
        else
        {
            destinationSlot = slot;
            PlaceResource(slot);
        }
    }

    public void OnDeselection(Slot slot)
    {
        destinationSlot = null;
    }

    public void Onselection(Resource resource)
    {
        // Only allow moving resources that are on the active player's board
        if (TurnManager.Instance.ActivePlayer.Board.Slots.Contains(resource.slot))
        {
            isMovingResource = true;
            resourceToMove = resource;
            HighlightValidMoveSlots();
        }
    }

    public void OnDeselection(Resource resource)
    {
        if (resource == resourceToMove)
        {
            ResetMoveState();
        }
    }

    private void HighlightValidMoveSlots()
    {
        UnhighlightAllSlots();
        availableMoveSlots.Clear();

        Board playerBoard = TurnManager.Instance.ActivePlayer.Board;
        List<Slot> playerOccupiedSlots = playerBoard.GetOccupiedSlots();
        // We don't want to move the selected piece next to itself
        playerOccupiedSlots.Remove(resourceToMove.slot);

        foreach (Slot slot in playerBoard.Slots)
        {
            if (!slot.isOccupied)
            {
                List<Slot> adjacentSlots = BoardManager.GetAdjacentSlots(slot);
                // Check if any adjacent slots on the *same board* are occupied
                bool isAdjacentToOccupiedOnBoard = adjacentSlots.Any(adjSlot => playerOccupiedSlots.Contains(adjSlot));

                if (!isAdjacentToOccupiedOnBoard)
                {
                    slot.Highlight();
                    availableMoveSlots.Add(slot);
                }
            }
        }
    }

    private void UnhighlightAllSlots()
    {
        foreach (Slot slot in TurnManager.Instance.ActivePlayer.Board.Slots)
        {
            slot.UnHighlight();
        }
    }

    private void ResetMoveState()
    {
        isMovingResource = false;
        resourceToMove = null;
        UnhighlightAllSlots();
        availableMoveSlots.Clear();
    }

    private bool CanBePlaced(Slot slot)
    {
        Board playerBoard = TurnManager.Instance.ActivePlayer.Board;
        if (!playerBoard.Slots.Contains(slot))
        {
            DisplayManager.Instance.DeliverError("You can only place on your own slot");
            return false;
        }

        if (slot.isOccupied)
        {
            DisplayManager.Instance.DeliverError("This slot is already occupied.");
            return false;
        }

        List<Slot> neighbouringSlots = BoardManager.GetAdjacentSlots(slot);
        // Only check for adjacency against occupied slots on the current player's board
        foreach (Slot neighbor in neighbouringSlots)
        {
            if (playerBoard.Slots.Contains(neighbor) && neighbor.isOccupied)
            {
                DisplayManager.Instance.DeliverError("You can not place on a slot adjacent to another resource on your board.");
                return false;
            }
        }
        return true;
    }

    private void PlaceResource(Slot slot)
    {
        if (collectionResources.Count > 0)
        {
            if (selectedResourceType != null && collectionResources.Contains(selectedResourceType.resourceType))
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
            else
            {
                TurnManager.Instance.ActivePlayer.selectedSlots.Clear();
                DisplayManager.Instance.DeliverError("Please choose a resource to place");
            }
        }
        if (collectionResources.Count == 0)
        {
            Hide();
            TurnManager.Instance.ActivePlayer.hasFinishedSettingUp = true;
            CheckForCompletion();
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    /// <summary>
    /// Places the active bot's next setup resource the way a player would: on its own board, never next to
    /// another of its resources. Same-colour squares of the board's checkerboard are never adjacent, so the bot
    /// fills those first and always has room for all five. Returns false when there is nothing to place.
    /// </summary>
    public bool PlaceForBot()
    {
        Player player = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : null;
        if (player == null || !player.IsBotControlled || player.setUpCard == null || player.hasFinishedSettingUp ||
            collectionResources == null || collectionResources.Count == 0 || selectedResourceType == null)
            return false;
        Board board = player.Board;
        Slot slot = board.Slots
            .Where(candidate => candidate != null && !candidate.isOccupied &&
                !BoardManager.GetAdjacentSlots(candidate).Any(next => board.Slots.Contains(next) && next.isOccupied))
            .OrderBy(candidate => (candidate.Index / 8 + candidate.Index % 8) % 2)
            .ThenBy(candidate => candidate.Index)
            .FirstOrDefault();
        if (slot == null)
            return false;
        selectedResourceType.resourceType = collectionResources[0];
        destinationSlot = slot;
        PlaceResource(slot);
        return true;
    }

    public void CheckForCompletion()
    {
        if (PlayerManager.Instance.Players.All(p => p.hasFinishedSettingUp))
        {
            Hide();
            SelectionManager.Instance.RemoveSlotSelectionListener(this);
            SelectionManager.Instance.RemoveResourceSelectionListener(this);
        }
        else
        {
            ActionManager.Instance.SetAction(ActionManager.ActionState.ResourceSetup);
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
        if (TurnManager.Instance.ActivePlayer.setUpCard != null && !TurnManager.Instance.ActivePlayer.hasFinishedSettingUp)
        {
            setupCard = TurnManager.Instance.ActivePlayer.setUpCard;
            collectionResources = new List<ResourceType>(setupCard.collectionResources);
            Display();
        }
        else
        {
            Hide();
        }
    }
}