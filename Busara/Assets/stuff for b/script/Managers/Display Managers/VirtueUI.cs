using UnityEngine;
using TMPro;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueUIre;
    public TMP_Text VirtueName;
    public TMP_Text NumberOFvirtues;

    public void Getvirtue()
    {

        TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueUIre);
        /*int virtuecount = 0;
        if (TurnManager.Instance.ActivePlayer.state.VirtuePrefab)
        {
            foreach (Virtue virtue in TurnManager.Instance.ActivePlayer.selectedVirtue)
            {
                if (virtueUIre = virtue)
                {
                    virtuecount++;
                }
            }
            if (TurnManager.Instance.ActivePlayer.selectedVirtue.Contains(virtueUIre))
                TurnManager.Instance.ActivePlayer.selectedVirtue.Remove(virtueUIre);
            else
                TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueUIre);
        }
        else
            Debug.Log("you should only chose your virtue");*/
    }

}
