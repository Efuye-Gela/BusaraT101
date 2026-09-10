using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Kingdom/Catalog")]
public class KingdomCatalog : ScriptableObject
{
    public List<Kingdom> kingdoms = new List<Kingdom>();
}
