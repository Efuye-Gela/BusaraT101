using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class dragDrop : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IEndDragHandler, IDragHandler {

    [SerializeField] public RectTransform m_RectTransform;
    [SerializeField] public Vector3 initialPosition;
    private CanvasGroup m_CanvasGroup;
    private void Awake()
    {
        m_RectTransform = GetComponent<RectTransform>();
        m_CanvasGroup = GetComponent<CanvasGroup>();

      initialPosition = m_RectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log("drag around");
        m_CanvasGroup.alpha = .6f;
        m_CanvasGroup.blocksRaycasts = false;
    }
    public void OnDrag(PointerEventData eventData)
    {
        Debug.Log("drag");
        m_RectTransform.anchoredPosition += eventData.delta; 
    }
   
    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log("End the drag");
        m_CanvasGroup.alpha = 1f;
        m_CanvasGroup.blocksRaycasts = true;
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log("OnPointerDown");
    }

}
