using UnityEngine;
using UnityEngine.UI;


[CreateAssetMenu(menuName ="Virtues", fileName ="New Virtue")]
public class Virtue : ScriptableObject
{
   public Sprite virtueIcon;
   public Resource ResourceOne;
   public Resource ResourceTwo;
   public VirtueType type;
   public ResourceType componentOne;
   public ResourceType componentTwo;
}
