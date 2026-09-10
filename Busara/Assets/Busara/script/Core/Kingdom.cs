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
    [System.Serializable]
    public class VirtuesForCost
    {
       public int NumberofVirtues;
       public Virtue virtues;
    }
}
