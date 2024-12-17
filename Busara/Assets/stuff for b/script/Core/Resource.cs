using UnityEngine;

public class Resource : MonoBehaviour
{
    public int index;
    public ResourceType resourceType;
    public Sprite resourceIcon;
    public Color pieceColor;
    public Slot slot;

    public Resource(ResourceType type)
    {
            resourceType = type;
    }

    
}
