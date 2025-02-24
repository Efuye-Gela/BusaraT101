using UnityEngine;
using UnityEngine.UI;

public class Resource : MonoBehaviour,SelectionManager.ResourceSelectionListener
{
    public int index;
    public ResourceType resourceType;
    public Sprite resourceIcon;
    public Color pieceColor;
    public Slot slot;
    [SerializeField] Image highlightImage;

    private Player currentPlayer => TurnManager.Instance.ActivePlayer;

    private void Start()
    {
        SelectionManager.Instance.AddResourceSelectionListener(this);
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
        resource.Highlight();
    }

    public void Highlight()
    {
        this.highlightImage.gameObject.SetActive(true);
    }

    public void UnHighlight()
    {
        this.highlightImage.gameObject.SetActive(false);
    }
}
