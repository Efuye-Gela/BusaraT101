using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "Kingdom/New")]
public class Kingdom : ScriptableObject
{
    public string kingdomName;

    [TextArea]
    public string kingdomStory;

    public Power power;

    [SerializeField]
    public VirtuesForCost[] virtuesForWin;
    protected Kingdom(string kingdomName, string kingdomStory, Power power, VirtuesForCost[] virtuesForWin)
    {
        this.kingdomName = kingdomName;
        this.kingdomStory = kingdomStory;
        this.power = power;
        this.virtuesForWin = virtuesForWin;

    }
    [System.Serializable]
    public class VirtuesForCost
    {
       
       public int NumberofVirtues;
       public Virtue virtues;
    }
}
