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
    public void NotifyWeaponUsed()
    {
        foreach(IWeaponUsed w in weaponUsed)
        {
            w.WeaponActivated();
        }
    }
    public interface IWeaponUsed
    {
        public void WeaponActivated();
    }
    public void OnTapUseWeapon()
    {
        if (TurnManager.Instance.ActivePlayer.selectedResources[0].slot.GetPlayer() != TurnManager.Instance.ActivePlayer)
        {
            DisplayManager.Instance.Deliver("You must start with your resource first");
            return;
        }
        if (!ActionManager.Instance.CanPerformAction())
        {
            DisplayManager.Instance.Deliver("You have already performed an action this turn.");
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
                    UseWeapon(tobeWeaponizedResources);
                }
                else
                {
                    DisplayManager.Instance.Deliver("Selected resource must be similar to activate weapon");
                }
            }
            else
            {
                DisplayManager.Instance.Deliver("You must select 3 resource to Use Weapon");
            }
        }

    }

    private void UseWeapon(List<Resource> resources)
    {
        foreach (Resource resource in resources) 
        {
            Slot removerSlot = resource.slot;
            removerSlot.EmptySlot();
        }

        Player player = TurnManager.Instance.ActivePlayer;
        TurnManager.Instance.ActivePlayer.selectedResources.Clear();
        List<Player> otherPlayers = new List<Player>(PlayerManager.Instance.Players);
        otherPlayers.Remove(player);
        ActionManager.Instance.SetAction(ActionManager.ActionState.UsedWeapon);
        TurnManager.Instance.OnSpecialTurn(false,otherPlayers);
        NotifyWeaponUsed();
    }
}
