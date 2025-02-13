using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ResourceCard : Card
{
    public ResourceType Resource;

    public ResourceCard(string cardName, string description, ResourceType resource)
        : base(cardName, description)
    {
        Resource = resource;
    }
}

[System.Serializable]
public enum ResourceType
{ 
    Water,
    Earth,
    Fire,
    Air
}