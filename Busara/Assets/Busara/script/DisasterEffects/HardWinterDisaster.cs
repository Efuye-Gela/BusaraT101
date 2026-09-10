using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HardWinterDisaster : DisasterEffect, TurnManager.TurnBeginListener, TurnManager.ISpecialTurnEndListeners
{
    public static HardWinterDisaster Active { get; private set; }
    public int DiscardRevision { get; private set; }
    public static event Action OnDiscardStateChanged;
    private readonly HashSet<Player> pendingPlayers = new HashSet<Player>();

    public override List<Player> GetAffectedPlayers()
    {
        // This disaster affects all players who have at least one virtue.
        return PlayerManager.Instance.Players.Where(p => p.Virtues.Count > 0).ToList();
    }

    public override void Execute(List<Player> affectedPlayers, Action onDisasterComplete)
    {
        if (Active != null || TurnManager.Instance.isSpecialCardDrawn)
        {
            Debug.LogError("Finish the current special turn before starting a virtue discard.");
            return;
        }
        List<Player> participants = affectedPlayers
            .Where(player => player != null && PlayerManager.Instance.Players.Contains(player) &&
                player.Virtues.Count > 0).Distinct().ToList();
        if (participants.Count == 0)
        {
            DisplayManager.Instance.DeliverInstructions("A hard winter strikes, but no one had any virtues to lose.");
            onDisasterComplete?.Invoke();
            return;
        }
        if (!DisplayManager.Instance.ShowPlayerInfo())
            return;

        pendingPlayers.UnionWith(participants);
        DiscardRevision++;
        Active = this;
        TurnManager.Instance.AddTurnBeginListeners(this);
        TurnManager.Instance.AddSpecialTurnEndListeners(this);
        TurnManager.Instance.OnSpecialTurn(false, participants, onDisasterComplete);
    }

    public bool RequiresDiscard(Player player)
    {
        return player != null && pendingPlayers.Contains(player);
    }

    public bool CanDiscard(Player player, Virtue virtue)
    {
        return Active == this && TurnManager.Instance != null &&
            TurnManager.Instance.isSpecialCardDrawn && TurnManager.Instance.ActivePlayer == player &&
            RequiresDiscard(player) && virtue != null &&
            player.Virtues.Any(owned => owned != null && owned.type == virtue.type) &&
            (PowerManager.Instance == null || !PowerManager.Instance.IsBusy);
    }

    public bool TryDiscard(Player player, Virtue virtue)
    {
        if (!CanDiscard(player, virtue))
        {
            Debug.LogWarning("Only the current affected player may discard one owned virtue; finish pending choices first.");
            return false;
        }

        // Remove an actual owned token, not the display asset or a power-payment selection.
        Virtue owned = player.Virtues.First(token => token != null && token.type == virtue.type);
        player.Virtues.Remove(owned);
        pendingPlayers.Remove(player);
        Debug.Log($"{player.Name} discarded the virtue: {owned.name}");
        OnDiscardStateChanged?.Invoke();
        TurnManager.Instance.CompleteSpecialTurn(player);
        return true;
    }

    public void OnTurnBegin()
    {
        if (Active != this)
            return;
        DisplayManager.Instance.ShowPlayerInfo();
        DisplayManager.Instance.DeliverInstructions(
            $"Hard winter: {TurnManager.Instance.ActivePlayer.Name}, click X beside one virtue to discard it.");
        OnDiscardStateChanged?.Invoke();
    }

    public void OnSpecialTurnEnd()
    {
        ClearDiscardState();
    }

    private void OnDisable()
    {
        ClearDiscardState();
    }

    private void ClearDiscardState()
    {
        if (TurnManager.Instance != null)
        {
            TurnManager.Instance.RemoveTurnBeginListener(this);
            TurnManager.Instance.RemoveSpecialTurnEndListeners(this);
        }
        pendingPlayers.Clear();
        if (Active != this)
            return;
        Active = null;
        OnDiscardStateChanged?.Invoke();
    }
}
