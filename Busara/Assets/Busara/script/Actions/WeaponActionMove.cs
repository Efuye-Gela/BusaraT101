using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponActionMove : MonoBehaviour
{
    public static WeaponActionMove Instance {  get; private set; }
    private void Start()
    {
        if(Instance == null)
            Instance = this;
        else
            Destroy(Instance);
    }
    private List<Resource> tobeWeaponizedResources = new List<Resource>();
    List<IWeaponUsed> weaponUsed = new List<IWeaponUsed>();

    public void AddWeaponUsedListeners(IWeaponUsed weaponUsed)
    {
        if (!this.weaponUsed.Contains(weaponUsed))
        {
            this.weaponUsed.Add(weaponUsed);
        }
    }
    public void RemoveWeaponUsedListeners(IWeaponUsed weaponUsed)
    {
        if (this.weaponUsed.Contains(weaponUsed))
        {
            this.weaponUsed.Remove(weaponUsed);
        }
    }
    public void NotifyWeaponUsed(Dictionary<Player, int> DiscardedResource)
    {
        foreach(IWeaponUsed w in weaponUsed)
        {
            w.WeaponActivated(DiscardedResource);
        }
    }
    public interface IWeaponUsed
    {
        public void WeaponActivated(Dictionary<Player, int> DiscardedResource);
    }
    public void OnTapUseWeapon()
    {
        if (TurnManager.Instance.ActivePlayer.selectedResources.Count == 0)
        {
            DisplayManager.Instance.DeliverError("Select three resources to use a weapon.");
            return;
        }
        if (TurnManager.Instance.ActivePlayer.selectedResources[0].slot.GetPlayer() != TurnManager.Instance.ActivePlayer)
        {
            DisplayManager.Instance.DeliverError("You must start with your resource first");
            return;
        }
        if (!ActionManager.Instance.CanPerformAction())
        {
            DisplayManager.Instance.DeliverError("You have already performed an action this turn.");
            return;
        }
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (currentPlayer != null) 
        {
            tobeWeaponizedResources = currentPlayer.selectedResources;
            List<ResourceType> resourceTypes = new List<ResourceType>();
            if (tobeWeaponizedResources.Count == 3)
            {
                bool sameResourceType = false;
                sameResourceType = tobeWeaponizedResources.Select(x => x.resourceType).Distinct().Count() == 1;
                if (sameResourceType)
                {
                    if (AreResourcesAdjacent(tobeWeaponizedResources))
                    {
                        UseWeapon(tobeWeaponizedResources);
                    }
                    else
                    {
                        DisplayManager.Instance.DeliverError("Weapon resources must be adjacent to each other.");
                    }
                }
                else
                {
                    DisplayManager.Instance.DeliverError("Selected resource must be similar to activate weapon");
                }
            }
            else
            {
                DisplayManager.Instance.DeliverError("You must select 3 resource to Use Weapon");
            }
        }

    }
    private bool AreResourcesAdjacent(List<Resource> resources)
    {
        if (resources.Count != 3) return false;

        Resource res1 = resources[0];
        Resource res2 = resources[1];
        Resource res3 = resources[2];

        // Check how many adjacency connections exist between the three resources.
        // A connected group of 3 requires at least 2 connections (e.g., a line 1-2-3 or a cluster 2-1-3).
        int connectionCount = 0;
        if (res1.IsAdjacentTo(res2)) connectionCount++;
        if (res1.IsAdjacentTo(res3)) connectionCount++;
        if (res2.IsAdjacentTo(res3)) connectionCount++;

        return connectionCount >= 2;
    }

    private void UseWeapon(List<Resource> resources)
    {
        foreach (Resource resource in resources) 
        {
            Slot removerSlot = resource.slot;
            removerSlot.EmptySlot();
            resource.gameObject.SetActive(false);
            Destroy(resource.gameObject);
        }

        Player player = TurnManager.Instance.ActivePlayer;
        TurnManager.Instance.ActivePlayer.selectedResources.Clear();

        // Get all other players
        List<Player> otherPlayers = new List<Player>(PlayerManager.Instance.Players);
        otherPlayers.Remove(player);

        // Filter the list to only include players who have resources on their board
        List<Player> targetPlayers = otherPlayers.Where(p => p.Board.GetOccupiedSlots().Count > 0).ToList();

        ActionManager.Instance.SetAction(ActionManager.ActionState.UsedWeapon);
        OfferProtection(player, targetPlayers, 0);
    }

    private void OfferProtection(Player attacker, List<Player> targets, int index)
    {
        if (index < targets.Count && PowerManager.Instance != null)
        {
            Player defender = targets[index];
            PowerManager.Instance.OfferProtection(defender, $"{defender.Name} is targeted by an attack.",
                () =>
                {
                    targets.RemoveAt(index);
                    OfferProtection(attacker, targets, index);
                },
                () => OfferProtection(attacker, targets, index + 1), isAttack: true, target: attacker);
            return;
        }

        Dictionary<Player, int> discardsRequired = new Dictionary<Player, int>();
        // Only start a special turn if there are players to target
        if (targets.Count > 0)
        {
            foreach (Player p in targets)
            {
                discardsRequired[p] = 1;
            }
            TurnManager.Instance.OnSpecialTurn(false, targets);
            NotifyWeaponUsed(discardsRequired);
        }
        else
        {
            // If no players have resources, just end the current player's turn
            TurnManager.Instance.CompleteTurn(attacker);
        }
    }
}
