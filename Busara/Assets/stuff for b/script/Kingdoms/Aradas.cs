using UnityEngine;


[CreateAssetMenu(menuName = "Kingdom/Aradas")]
public class Aradas : Kingdom
{
    protected Aradas(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues) :
      base(kingdomName, kingdomStory, power, virtues)
    {

    }
}
