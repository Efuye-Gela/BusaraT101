using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Selectable : MonoBehaviour, IPointerDownHandler
{
    private RectTransform rectTransform;
    public Canvas canvas;
    private CanvasGroup canvasGroup;
    public static Action<GameObject> OnDraggableClicked;
    public Action selectedResourcesChanged;
    public List<Resource> selectedResources = null;
    public void OnPointerDown(PointerEventData eventData)
    {
        Resource clickedOnResource = this.gameObject.GetComponent<Resource>();
        if (clickedOnResource != null)
            SelectionManager.Instance.ToggleSelect(clickedOnResource);
        else
            Debug.Log("...");
    }
}
