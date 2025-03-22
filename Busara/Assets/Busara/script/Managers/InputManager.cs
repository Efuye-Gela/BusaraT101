using UnityEngine;
using UnityEngine.EventSystems;

public class InputManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public Board Board;
    public Player player;

    public void OnDrag(PointerEventData eventData)
    {

    }

    public void OnPointerDown(PointerEventData eventData)
    {
        GameObject clickedObject = eventData.pointerCurrentRaycast.gameObject;
        if (clickedObject != null && isResource(clickedObject))
        {   
            
        }

    }

    public void OnPointerUp(PointerEventData eventData)
    {

    }

    private bool isResource(GameObject gameObject)
    {
        if (gameObject.GetComponentInParent<Resource>() != null)
            return true;
        else
            return false;
    }
}
