using UnityEngine;
using UnityEngine.UI;


[CreateAssetMenu(menuName ="Virtues", fileName ="New Virtue")]
public class Virtue : ScriptableObject
{
   public Image virtueIcon;
    
   public VirtueType type;
   public ResourceType componentOne;
   public ResourceType componentTwo;
}

public enum VirtueType
{ 
    Art,
    Security,
    Wisdom,
    Energy,
    Economy,
    Nature
}