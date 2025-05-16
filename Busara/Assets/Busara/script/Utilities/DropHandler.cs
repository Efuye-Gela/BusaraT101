using System;
using UnityEngine;
using UnityEngine.EventSystems;


public class DropHandler : MonoBehaviour, IDropHandler, IPointerDownHandler
{
    public static Action<Resource,Slot> OnItemPlaced;
    public static Action<GameObject> OnSlotClicked;

    public  void OnDrop(PointerEventData eventData)
    {
 /*       //Debug.Log("OnDrop");
        if (eventData.pointerDrag != null)
        {
            GameObject currentGameObject = eventData.pointerDrag.gameObject;
            //eventData.pointerDrag.GetComponent<RectTransform>().localPosition = GetComponent<RectTransform>().anchoredPosition;
            currentGameObject.transform.SetParent(GetComponent<RectTransform>().gameObject.transform,true);
            currentGameObject.transform.localPosition = Vector3.zero;
            OnItemPlaced?.Invoke(currentGameObject.GetComponent<Resource>(),this.GetComponent<Slot>());
        }*/
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        //OnSlotClicked?.Invoke(this.gameObject);
        Slot clickedOnSlot = this.gameObject.GetComponent<Slot>();
        if (clickedOnSlot != null)
            SelectionManager.Instance.ToggleSelect(clickedOnSlot);
        else
            Debug.Log("...");
    }
}
