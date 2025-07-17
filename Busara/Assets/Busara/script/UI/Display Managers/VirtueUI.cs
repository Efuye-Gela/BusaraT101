using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueType;
    public Player currentPlayer;
    public Image virtueImage;
    public Transform ResourcePanel;
    public Image ResourceOneImage;
    public Image ResourceTwoImage;
    public TMP_Text VirtueName;
    public TMP_Text NumberOfvirtues;
    public GameObject IncDecButtons;
    public void SetResourceCombo()
    {
        if(ResourcePanel != null && ResourceOneImage != null && ResourceTwoImage != null)
        {
            ResourcePanel.gameObject.SetActive(true);
            ResourceOneImage.sprite = virtueType.ResourceOne.resourceIcon;
            ResourceTwoImage.sprite = virtueType.ResourceTwo.resourceIcon;
        }

    }

    public void AddVirtue()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;

        Dictionary<VirtueType, int> TempVirtue = TurnManager.Instance.ActivePlayer.state.virtueCounts;
        if (TempVirtue.ContainsKey(virtueType.type) && TempVirtue[virtueType.type] > 0)
        {
            int selectedCount = TurnManager.Instance.ActivePlayer.selectedVirtue.Count(v => v.type == virtueType.type);
            if (selectedCount < TempVirtue[virtueType.type])
            {
                TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueType);
                Debug.Log($"Virtue {virtueType.name} added to selected virtues.");

                Debug.Log($"selected {virtueType.name}. Total virtue selected {TurnManager.Instance.ActivePlayer.selectedVirtue.Count}.");

            }
            else
            {
                Debug.Log($"Cannot add more {virtueType.name}, maximum allowed based on available virtues reached.");
            }
        }
        else
        {
            Debug.Log($"Player does not have {virtueType.name} to add.");  
        }
    }
    public void RemoveVirtue()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;


        if (TurnManager.Instance.ActivePlayer.selectedVirtue.Contains(virtueType))
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Remove(virtueType);
            Debug.Log($"Virtue {virtueType.name} removed from selected virtues.");
        }
        else
        {
            Debug.Log($"Cannot remove {virtueType.name} as it is not in selected virtues.");
        }
    }

    public void TurnOnINCDEC()
    {
        IncDecButtons.SetActive(true);
    }
    public void TurnOffINCDEC()
    {
        IncDecButtons.SetActive(false);
    }

}
