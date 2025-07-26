using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Resource : MonoBehaviour,SelectionManager.ResourceSelectionListener, WeaponActionMove.IWeaponUsed, TurnManager.ISpecialTurnEndListeners
{
    public int index;
    public ResourceType resourceType;
    public Sprite resourceIcon;
    public Color pieceColor;
    public Slot slot;
    [SerializeField] Image highlightImage;
    public Transform DeleteBtn;

    private void OnEnable()
    {
        if (WeaponActionMove.Instance != null)
             WeaponActionMove.Instance.AddWeaponUsedListeners(this);
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddSpecialTurnEndListeners(this);

        ResourceDisaster.OnResourceDisaster += ResourceDisaster_OnResourceDisaster;
        CorruptionDisaster.OnCorruptionDisaster += CorruptionDisaster_OnCorruptionDisaster;
    }

    static Dictionary<Player, int> ResourceCount;
    private void CorruptionDisaster_OnCorruptionDisaster(Dictionary<Player, int> Count)
    {
        TurnOnDeleteBtn();
        ResourceCount = Count;
    }
    private void ResourceDisaster_OnResourceDisaster()
    {
        TurnOnDeleteBtn();
    }

    private void OnDisable()
    {
        if (WeaponActionMove.Instance != null)
            WeaponActionMove.Instance.RemoveWeaponUsedListeners(this);
        if (TurnManager.Instance != null)
            TurnManager.Instance.RemoveSpecialTurnEndListeners(this);
        ResourceDisaster.OnResourceDisaster -= ResourceDisaster_OnResourceDisaster;
    }
    private void Start()
    {
        SelectionManager.Instance.AddResourceSelectionListener(this);
    }
    public void TurnOnDeleteBtn()
    {
        DeleteBtn.gameObject.SetActive(true);
    }
    public void TurnOffDeleteBtn()
    {
        DeleteBtn.gameObject.SetActive(false);
    }
    public void OnclickDestroy()
    {
        if (!TurnManager.Instance.ActivePlayer.Board.Slots.Contains(slot)) return;


        if (ResourceCount[TurnManager.Instance.ActivePlayer] - 1 > 0)
        {
            slot.EmptySlot();
            Destroy(gameObject);
            ResourceCount[TurnManager.Instance.ActivePlayer]--;
        }
        else
        {
            slot.EmptySlot();
            Destroy(gameObject);
            TurnManager.Instance.CompleteSpecialTurn(TurnManager.Instance.ActivePlayer);
        }

    }
    public bool IsAdjacentTo(Resource secondResource)
    { 
       List<Slot> adjcentSlots = BoardManager.GetAdjacentSlots(slot);
        foreach (Slot slot in adjcentSlots)
        {
            if (slot == secondResource.slot)
            {
                return true;
            }
        }
        return false;
    }

    public Resource(ResourceType type)
    {
          resourceType = type;
    }

    public void OnDeselection(Resource resource)
    {
            resource.UnHighlight();
    }

    public void Onselection(Resource resource)
    {
        if (TurnManager.Instance.ActivePlayer.selectedResources.Contains(resource))
            resource.Highlight();
    }

    public void Highlight()
    {
        this.highlightImage.gameObject.SetActive(true);
    }

    public void UnHighlight()
    {
        if(this.highlightImage != null)
            this.highlightImage.gameObject.SetActive(false);
    }

    public void WeaponActivated()
    {
        TurnOnDeleteBtn();
    }
    public void OnSpecialTurnEnd()
    {
        TurnOffDeleteBtn();
        ActionManager.Instance.ResetActionState();
    }
}
