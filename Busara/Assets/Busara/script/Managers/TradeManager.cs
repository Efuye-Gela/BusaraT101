using System.Collections.Generic;
using System;
using UnityEngine;
using System.Linq;

public class TradeManager : Manager<TradeManager>
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

    public void OfferTrade()
    {
        //TODO: Implement if there are more than one resource to be traded out
        Resource selectedResource = tobeTradedOutResources[0];
        sourceSlot = selectedResource.slot;

        if (tobeTradedOutResources.Select(x => x.resourceType).Distinct().Count() == 1)
        {
            ResourceType type = tobeTradedOutResources.Select(x => x.resourceType).Distinct().First();
            List<ResourceType> otherTypes = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().ToList();
            otherTypes.Remove(type);
            Tuple<ResourceType, List<ResourceType>> offerTuple = new Tuple<ResourceType, List<ResourceType>>(type, otherTypes);

            TradeInitiated?.Invoke(offerTuple);
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
        TurnManager.Instance.OnSpecialTurn(true,otherPlayers);
        TradeCreated?.Invoke(_offerTuple);
    }

    public bool CheckForResources() 
    {
        Board currentPlayerBoard = TurnManager.Instance.ActivePlayer.Board;
        List<Slot> occupiedSlots = currentPlayerBoard.GetOccupiedSlotByResourceType(_offerTuple.Item2);
        if (occupiedSlots.Count > 0)
            return true;
        else
            return false;
    }

    public void PlayerAcceptsTradeOffer()
    {
        acceptedPlayers.Add(TurnManager.Instance.ActivePlayer);
        int totalPlayersCount = acceptedPlayers.Distinct().ToList().Count + rejectedPlayers.Distinct().ToList().Count;
        if (totalPlayersCount == PlayerManager.Instance.Players.Count - 1)
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer,OnTradeOfferCompleted);
        }
        else
        { 
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
    }

    private void OnTradeOfferCompleted()
    {
        TradeOfferCompleted?.Invoke();
    }

    public void PlayerRejectsTradeOffer()
    {
        rejectedPlayers.Add(TurnManager.Instance.ActivePlayer);
        int totalPlayersCount = acceptedPlayers.Distinct().ToList().Count + rejectedPlayers.Distinct().ToList().Count;
        if (totalPlayersCount == PlayerManager.Instance.Players.Count - 1)
        {
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer, OnTradeOfferCompleted);
        }
        else
        { 
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }

    }

    public void PlayerSelectsTradePartner(Player player)
    {
        tradePartner = player; 
    }

    public void TradeResources()
    {
        List<Player> tradePlayer = new List<Player>();
        if (tradePartner!=null)
        {
            if (tradePartner.Board.GetOccupiedSlotByResourceType(_offerTuple.Item2).Count > 1)
            {
                tradePlayer.Add(tradePartner);
                TurnManager.Instance.OnSpecialTurn(true, tradePlayer);
                TradeTobeCompleted?.Invoke();
            }
            else
            {

                StartResourceSwap(tradePartner);
                TradeCompleted?.Invoke();
            }
        }
        else
            TradeFailed?.Invoke();
    }

    private void StartResourceSwap(Player player)
    {

       List <Slot> occupiedSlots = player.Board.GetOccupiedSlotByResourceType(_offerTuple.Item2);

        if (occupiedSlots.Count == 1)
            targetSlot = occupiedSlots[0].resource.slot;
        else
            targetSlot = null;

        SwapResources(sourceSlot,targetSlot);
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
        GameObject tradedOutResourceClone = Instantiate(tradedOutResource.gameObject);
        Resource tradedInResource = targetSlot.resource;
        GameObject tradedInResourceClone = Instantiate(tradedInResource.gameObject);

        targetSlot.EmptySlot();
        Resource resourceOut = tradedOutResourceClone.GetComponent<Resource>();
        Slot.OccupySlot(targetSlot, resourceOut);
        tradedOutResourceClone.transform.SetParent(targetSlot.gameObject.transform,true);
        tradedOutResourceClone.transform.localPosition = Vector3.zero;
        tradedOutResourceClone.transform.localScale = Vector3.one;

        sourceSlot.EmptySlot();
        Resource resourceIn = tradedInResourceClone.GetComponent<Resource>();
        Slot.OccupySlot(sourceSlot, resourceIn);
        tradedInResourceClone.transform.SetParent(sourceSlot.gameObject.transform,true);
        tradedInResourceClone.transform.localPosition = Vector3.zero;
        tradedInResourceClone.transform.localScale = Vector3.one;

        //TurnManager.Instance.ActivePlayer.selectedResources.Clear();
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

}
