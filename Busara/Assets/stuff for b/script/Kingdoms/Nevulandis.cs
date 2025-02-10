using UnityEngine;


[CreateAssetMenu(menuName = "Kingdom/Nevulandis")]
public class Nevulandis : Kingdom
{
    protected Nevulandis(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtues) :
      base(kingdomName, kingdomStory, power, virtues)
    {

    }
}
