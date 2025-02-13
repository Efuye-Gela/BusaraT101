using UnityEngine;


[CreateAssetMenu(menuName = "Kingdom/EkoAkete")]
public class EkoAkete : Kingdom
{
    protected EkoAkete(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues) :
    base(kingdomName, kingdomStory, power, virtues)
    {

    }
}
