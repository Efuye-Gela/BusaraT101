using System.Collections.Generic;
using UnityEngine;
using static Kingdom;

[CreateAssetMenu(menuName = "Kingdom/RoyaumeKongo")]
public class RoyaumeKongo : Kingdom
{
    protected RoyaumeKongo(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues):
        base(kingdomName, kingdomStory, power, virtues)
    {

    }
}
