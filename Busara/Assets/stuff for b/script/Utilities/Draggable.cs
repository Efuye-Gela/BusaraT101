using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class Draggable : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler
{
    private RectTransform rectTransform;
    public Canvas canvas;
    private CanvasGroup canvasGroup;
    public static Action<GameObject> OnDraggableClicked;
    public Action selectedResourcesChanged;
    public List<Resource> selectedResources = null;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
    }
    private void Start()
    {
        selectedResourcesChanged += DisplaySelectedResources;
        ForgeManager.Instance.selectedResourceCleared += DisplaySelectedResources;
    }



    public void OnBeginDrag(PointerEventData eventData)
    {
        // Debug.Log("OnBeginDrag");
        canvasGroup.alpha = .6f;
        canvasGroup.blocksRaycasts = false;

    }

    public void OnDrag(PointerEventData eventData)
    {
        rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        //Debug.Log("OnEndDrag");
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        //Debug.Log("OnPointerDown");
        OnDraggableClicked?.Invoke(this.gameObject);
        selectedResources = TurnManager.Instance.ActivePlayer.selectedResources;

        Resource clickedOnResource = this.gameObject.GetComponent<Resource>();
        if (clickedOnResource != null)
        {
            if (selectedResources.Count == 0)
            {
                if (clickedOnResource.slot != null)
                {
                    selectedResources.Add(clickedOnResource);
                    selectedResourcesChanged?.Invoke();
                }

            }

            else if (selectedResources.Count > 0)
            {
                if (selectedResources.Contains(clickedOnResource))
                {
                    selectedResources.Remove(clickedOnResource);
                    selectedResourcesChanged?.Invoke();
                    clickedOnResource.slot.gameObject.GetComponent<Image>().color = Color.white;
                }

                else
                {

                    List<Slot> adjacentSlots = new List<Slot>();

                    Resource lastSelectedResource = selectedResources[selectedResources.Count - 1];
                    if (lastSelectedResource.slot != null)
                    {
                        adjacentSlots = BoardManager.GetAdjacentSlots(lastSelectedResource.slot);
                        if (adjacentSlots.Contains(clickedOnResource.slot) && clickedOnResource.slot != null)
                        {
                            selectedResources.Add(clickedOnResource);
                            selectedResourcesChanged?.Invoke();
                            DisplaySelectedResources();

                        }
                    }
                }
            }

        }
    }

    void DisplaySelectedResources()
    {
        foreach (var slot in TurnManager.Instance.ActivePlayer.Board.Slots)
        {
            if (selectedResources != null && selectedResources.Count > 0)
            {
                if (selectedResources.Contains(slot.resource))
                {
                    slot.gameObject.GetComponent<Image>().color = Color.yellow;
                }
                else
                    slot.gameObject.GetComponent<Image>().color = Color.white;

            }
        }


    }

}