using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class WeaponActionMove : MonoBehaviour
{
    private List<Resource> tobeWeaponizedResources = new List<Resource>();
    private void Start()
    {
        Draggable.OnDraggableClicked += IsValidAction;
    }


    private void IsValidAction(GameObject gameObject) 
    {
        //Resource clickedOnResource = gameObject.GetComponent<Resource>();
        //if (clickedOnResource != null)
        //{ 
        //    if (!tobeWeaponizedResources.Contains(clickedOnResource))
        //    {
        //        tobeWeaponizedResources.Add(clickedOnResource);
        //    }
        //}

    }

    public void OnTapUseWeapon()
    { 
       Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (currentPlayer != null) 
        {
            tobeWeaponizedResources = currentPlayer.selectedResources;
            List<ResourceType> resourceTypes = new List<ResourceType>();
            if (tobeWeaponizedResources.Count == 3)
            {
                bool sameResourceType = false;
                sameResourceType = tobeWeaponizedResources.Select(x => x.resourceType).Distinct().Count() == 1;
                if (sameResourceType) {
                    UseWeapon(tobeWeaponizedResources);
                }
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
        TurnManager.Instance.OnSpecialCardDrawn(false,otherPlayers);


    }
}
