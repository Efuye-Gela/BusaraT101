using UnityEngine;

[CreateAssetMenu(menuName = "Kingdom/Milu")]
public class Milu : Kingdom
{
    protected Milu(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues) :
     base(kingdomName, kingdomStory, power, virtues)
    {

    }

}
