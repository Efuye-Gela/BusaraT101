using UnityEngine;
using TMPro;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueUIre;
    public TMP_Text VirtueName;
    public TMP_Text NumberOFvirtues;

    public void GetVirtue()
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueUIre);
            //when i add this to a list does it just make a copy of the referance 
        }
    }
}
