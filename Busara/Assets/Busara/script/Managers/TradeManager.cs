using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TradeManager : Manager<TradeManager>, SelectionManager.ResourceSelectionListener
{
    public List<Resource> tobeTradedOutResources = new List<Resource>();
    public List<ResourceType> tobeTradedInResources = new List<ResourceType>();
    public Tuple<ResourceType, ResourceType> _offerTuple;
    public List<Player> acceptedPlayers = new List<Player>();
    public List<Player> rejectedPlayers = new List<Player>();

    public Action<Tuple<ResourceType, List<ResourceType>>> TradeInitiated;
    public Action<Tuple<ResourceType, ResourceType>> TradeCreated;
    public Action TradeCompleted;
    public Action TradeTobeCompleted;
    public Action TradeFailed;
    public Action TradeOfferCompleted;

    public Player tradePartner;

    private Slot sourceSlot;
    private Slot targetSlot;
    private bool waitingForResourceSelection = false;

    public void OfferTrade()
    {
        Resource selectedResource = tobeTradedOutResources[0];
        sourceSlot = selectedResource.slot;

        if (tobeTradedOutResources.Select(x => x.resourceType).Distinct().Count() == 1)
        {
            ResourceType type = tobeTradedOutResources.Select(x => x.resourceType).Distinct().First();
            List<ResourceType> otherTypes = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToList();
            otherTypes.Remove(type);
            _offerTuple = new Tuple<ResourceType, ResourceType>(type, otherTypes[0]);
            TradeInitiated?.Invoke(new Tuple<ResourceType, List<ResourceType>>(type, otherTypes));

            ActionManager.Instance.SetAction(ActionManager.ActionState.Traded);
        }
    }

    public void TradeOfferCreated()
    {
        string message = $"{TurnManager.Instance.ActivePlayer} wants to Trade {_offerTuple.Item1.ToString()} with {_offerTuple.Item2.ToString()} ";
        Debug.Log(message);
        ResetTradeStatus();
        List<Player> otherPlayers = new List<Player>(PlayerManager.Instance.Players);
        Player player = TurnManager.Instance.ActivePlayer;
        otherPlayers.Remove(player);
        TurnManager.Instance.OnSpecialTurn(true, otherPlayers);
        TradeCreated?.Invoke(_offerTuple);
    }

    public bool CheckForResources()
    {
        Board currentPlayerBoard = TurnManager.Instance.ActivePlayer.Board;
        List<Slot> occupiedSlots = currentPlayerBoard.GetOccupiedSlotByResourceType(_offerTuple.Item2);
        return occupiedSlots.Count > 0;
    }

    public void PlayerAcceptsTradeOffer()
    {
        acceptedPlayers.Add(TurnManager.Instance.ActivePlayer);
        int totalPlayersCount = acceptedPlayers.Distinct().Count() + rejectedPlayers.Distinct().Count();
        if (totalPlayersCount == PlayerManager.Instance.Players.Count - 1)
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer, OnTradeOfferCompleted);
        }
        else
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    private void OnTradeOfferCompleted()
    {
        TradeOfferCompleted?.Invoke();
    }

    public void PlayerRejectsTradeOffer()
    {
        rejectedPlayers.Add(TurnManager.Instance.ActivePlayer);
        int totalPlayersCount = acceptedPlayers.Distinct().Count() + rejectedPlayers.Distinct().Count();
        if (totalPlayersCount == PlayerManager.Instance.Players.Count - 1)
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer, OnTradeOfferCompleted);
        }
        else
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    public void PlayerSelectsTradePartner(Player player)
    {
        tradePartner = player;
    }

    public void TradeResources()
    {
        if (tradePartner != null)
        {
            List<Slot> matchingSlots = tradePartner.Board.GetOccupiedSlotByResourceType(_offerTuple.Item2);

            if (matchingSlots.Count == 1)
            {
                targetSlot = matchingSlots[0];
                StartResourceSwap(tradePartner);
                TradeCompleted?.Invoke();
            }
            else if (matchingSlots.Count > 1)
            {
                waitingForResourceSelection = true;
                SelectionManager.Instance.AddResourceSelectionListener(this);
                TradeTobeCompleted?.Invoke();
                DisplayManager.Instance.DeliverInstructions("Choose a resource player " + tradePartner.Name);
            }
            else
            {
                Debug.LogWarning("Trade partner has no matching resources!");
                TradeFailed?.Invoke();
            }
        }
        else
        {
            TradeFailed?.Invoke();
        }
       
    }

    private void StartResourceSwap(Player player)
    {
        List<Slot> occupiedSlots = player.Board.GetOccupiedSlotByResourceType(_offerTuple.Item2);

        if (occupiedSlots.Count == 1)
            targetSlot = occupiedSlots[0].resource.slot;
        else
            targetSlot = null;

        SwapResources(sourceSlot, targetSlot);
    }

    public void TradeSelectedResource()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (currentPlayer.selectedResources.Count == 1)
        {
            Resource resource = currentPlayer.selectedResources[0];
            targetSlot = resource.slot;
            SwapResources(sourceSlot, targetSlot);
        }
    }

    private void SwapResources(Slot sourceSlot, Slot targetSlot)
    {
        Resource tradedOutResource = sourceSlot.resource;
        GameObject tradedOutResourceClone = UnityEngine.Object.Instantiate(tradedOutResource.gameObject);
        Resource tradedInResource = targetSlot.resource;
        GameObject tradedInResourceClone = UnityEngine.Object.Instantiate(tradedInResource.gameObject);

        targetSlot.EmptySlot();
        Resource resourceOut = tradedOutResourceClone.GetComponent<Resource>();
        Slot.OccupySlot(targetSlot, resourceOut);
        tradedOutResourceClone.transform.SetParent(targetSlot.gameObject.transform, true);
        tradedOutResourceClone.transform.localPosition = Vector3.zero;
        tradedOutResourceClone.transform.localScale = Vector3.one;

        sourceSlot.EmptySlot();
        Resource resourceIn = tradedInResourceClone.GetComponent<Resource>();
        Slot.OccupySlot(sourceSlot, resourceIn);
        tradedInResourceClone.transform.SetParent(sourceSlot.gameObject.transform, true);
        tradedInResourceClone.transform.localPosition = Vector3.zero;
        tradedInResourceClone.transform.localScale = Vector3.one;

        TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
    }

    public void ResetTradeStatus()
    {
        acceptedPlayers.Clear();
        rejectedPlayers.Clear();
    }

    public void DisplayTradeStatus()
    {
        Debug.Log("Trade Status");
        Debug.Log($"Accepted Players: {string.Join(",", acceptedPlayers)}");
        Debug.Log($"Rejected Players: {string.Join(",", rejectedPlayers)}");
    }

    public void Onselection(Resource resource)
    {
        if (!waitingForResourceSelection)
            return;
        if (!tradePartner.Board.Slots.Contains(resource.slot))
            return;
        Player currentPlayer = TurnManager.Instance.ActivePlayer;

        if (resource.resourceType == _offerTuple.Item2)
        {
            Debug.Log("Trade resource selected: " + resource.resourceType);

            waitingForResourceSelection = false;
            SelectionManager.Instance.RemoveResourceSelectionListener(this);

            targetSlot = resource.slot;
            SwapResources(sourceSlot, targetSlot);
            TradeCompleted?.Invoke();
        }
        else
        {
            Debug.Log("Selected resource is not valid for trade.");
            DisplayManager.Instance.DeliverError("Please select a resource of type " + _offerTuple.Item2);
        }
    }

    public void OnDeselection(Resource resource)
    {
        // Not used
    }
    public void CancelTrade()
    {
        Debug.Log("Trade canceled by player.");

        // Remove selection listener if active
        if (waitingForResourceSelection)
        {
            waitingForResourceSelection = false;
            SelectionManager.Instance.RemoveResourceSelectionListener(this);
        }

        // Reset internal state
        acceptedPlayers.Clear();
        rejectedPlayers.Clear();
        tobeTradedOutResources.Clear();
        tobeTradedInResources.Clear();
        tradePartner = null;
        _offerTuple = null;
        sourceSlot = null;
        targetSlot = null;

        // Reset external managers
        ActionManager.Instance.ResetActionState();// Resets to default state
        TurnManager.Instance.CancelSpecialTurn(); // Optional if you're mid-special-turn

        // Provide feedback if needed
        DisplayManager.Instance.DeliverError("Trade has been canceled.");

        Debug.Log("Trade state fully reset.");
    }

}
