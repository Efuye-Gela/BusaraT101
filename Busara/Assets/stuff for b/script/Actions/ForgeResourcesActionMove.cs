using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class ForgeResourcesActionMove : MonoBehaviour
{

    
    private void Start()
    {
        //Draggable.OnDraggableClicked += ResourceClicked;
    }

    //private void ResourceClicked(GameObject gameObject)
    //{
    //    Resource clickedOnResource = gameObject.GetComponent<Resource>();
    //    List<Resource> selectedResources =  clickedOnResource.GetComponent<Draggable>().selectedResources;

    //    if (selectedResources.Count == 0 && clickedOnResource.slot!=null)
    //    { 
    //        selectedResources.Add(clickedOnResource);
    //    }

    //    else if (selectedResources.Count > 0)
    //    {
    //        List<Slot> adjacentSlots = new List<Slot>();

    //        Resource lastSelectedResource = selectedResources[selectedResources.Count - 1];
    //        if (lastSelectedResource.slot != null)
    //        { 
    //            adjacentSlots = BoardManager.GetAdjacentSlots(lastSelectedResource.slot);
    //            if (adjacentSlots.Contains(clickedOnResource.slot) && clickedOnResource.slot != null)
    //            {
    //                selectedResources.Add(clickedOnResource);

    //            }
    //        }

            
    //    }
    //}

    

    public void OnTapForge()
    {
        bool forged = ForgeManager.Instance.Forge();
        if (forged)
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        
    }



}
