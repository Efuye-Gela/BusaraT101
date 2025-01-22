using UnityEngine;

public class ResourceDisplayManager : MonoBehaviour
{
    public void MoveResourceUI(Slot slot)
    {
        Resource tobeMovedResource = slot.resource;
        tobeMovedResource.gameObject.transform.SetParent(slot.gameObject.transform);
        tobeMovedResource.gameObject.transform.localPosition = Vector3.zero;
    }


}
