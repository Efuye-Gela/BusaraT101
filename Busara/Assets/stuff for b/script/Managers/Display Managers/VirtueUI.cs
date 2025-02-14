using UnityEngine;
using TMPro;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueUIre;
    public TMP_Text VirtueName;
    public TMP_Text NumberOFvirtues;

    public void Getvirtue()
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueUIre);
        }
    }

}
