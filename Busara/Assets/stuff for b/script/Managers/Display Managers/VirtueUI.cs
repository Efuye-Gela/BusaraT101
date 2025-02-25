using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueUIre;
    public Image virtueImage;
    public TMP_Text VirtueName;
    public TMP_Text NumberOFvirtues;


    public void AddVirtue()
    {
        if (TurnManager.Instance.ActivePlayer == null) return;

        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        Dictionary<VirtueType, int> TempVirtue = currentPlayer.state.virtueCounts;
        if (TempVirtue.ContainsKey(virtueUIre.type) && TempVirtue[virtueUIre.type] > 0)
        {
            int selectedCount = currentPlayer.selectedVirtue.Count(v => v.type == virtueUIre.type);
            if (selectedCount < TempVirtue[virtueUIre.type])
            {
                currentPlayer.selectedVirtue.Add(virtueUIre);
                Debug.Log($"Virtue {virtueUIre.name} added to selected virtues.");
            }
            else
            {
                Debug.Log($"Cannot add more {virtueUIre.name}, maximum allowed based on available virtues reached.");
            }
        }
        else
        {
            Debug.Log($"Player does not have {virtueUIre.name} to add.");
        }
    }
    public void RemoveVirtue()
    {
        if (TurnManager.Instance.ActivePlayer == null) return;

        Player currentPlayer = TurnManager.Instance.ActivePlayer;

        if (currentPlayer.selectedVirtue.Contains(virtueUIre))
        {
            currentPlayer.selectedVirtue.Remove(virtueUIre);
            Debug.Log($"Virtue {virtueUIre.name} removed from selected virtues.");
        }
        else
        {
            Debug.Log($"Cannot remove {virtueUIre.name} as it is not in selected virtues.");
        }
    }

}
