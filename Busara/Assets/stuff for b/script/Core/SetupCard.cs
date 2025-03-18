using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
[CreateAssetMenu(menuName ="SetupCard")]
public class SetupCard : ScriptableObject
{
    public List<ResourceType> collectionResources = new List<ResourceType>();
}
