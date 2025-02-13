using UnityEngine;

public class SlotDisplayManager : MonoBehaviour
{

    private void Start()
    {
        Slot.OnSlotEmptied += EmptySlot;
    }


    public void EmptySlot(Slot slot)
    {
        Resource tobeMovedResource = slot.resource;
        Destroy(tobeMovedResource.gameObject);
    }
}
