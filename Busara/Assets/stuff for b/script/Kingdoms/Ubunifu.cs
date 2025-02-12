using UnityEngine;
using static Kingdom;

[CreateAssetMenu(menuName = "Kingdom/Ubunifu")]
public class Ubunifu : Kingdom
{
    protected Ubunifu(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues) :
     base(kingdomName, kingdomStory, power, virtues)
    {

    }
}
