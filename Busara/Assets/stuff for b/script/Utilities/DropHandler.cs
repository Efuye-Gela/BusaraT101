using System;
using UnityEngine;
using UnityEngine.EventSystems;


public class DropHandler : MonoBehaviour, IDropHandler, IPointerDownHandler
{
    public static Action<Resource,Slot> OnItemPlaced;
    public static Action<GameObject> OnSlotClicked;

    public  void OnDrop(PointerEventData eventData)
    {
        //Debug.Log("OnDrop");
        if (eventData.pointerDrag != null)
        {
            GameObject currentGameObject = eventData.pointerDrag.gameObject;
            //eventData.pointerDrag.GetComponent<RectTransform>().localPosition = GetComponent<RectTransform>().anchoredPosition;
            currentGameObject.transform.SetParent(GetComponent<RectTransform>().gameObject.transform);
            currentGameObject.transform.localPosition = Vector3.zero;
            OnItemPlaced?.Invoke(currentGameObject.GetComponent<Resource>(),this.GetComponent<Slot>());
        }
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(this.gameObject);
    }
}
