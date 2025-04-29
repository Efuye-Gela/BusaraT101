using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class VirtueUI : MonoBehaviour
{
    public Virtue virtueUIre;
    public Player currentPlayer;
    public Image virtueImage;
    public TMP_Text VirtueName;
    public TMP_Text NumberOfvirtues;
    public GameObject IncDecButtons;
    public VirtueUI instance;

    private void Start()
    {
        instance = this;
    }

    public void AddVirtue()
    {
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;

        Dictionary<VirtueType, int> TempVirtue = TurnManager.Instance.ActivePlayer.state.virtueCounts;
        if (TempVirtue.ContainsKey(virtueUIre.type) && TempVirtue[virtueUIre.type] > 0)
        {
            int selectedCount = TurnManager.Instance.ActivePlayer.selectedVirtue.Count(v => v.type == virtueUIre.type);
            if (selectedCount < TempVirtue[virtueUIre.type])
            {
                TurnManager.Instance.ActivePlayer.selectedVirtue.Add(virtueUIre);
                Debug.Log($"Virtue {virtueUIre.name} added to selected virtues.");

                Debug.Log($"selected {virtueUIre.name}. Total virtue selected {TurnManager.Instance.ActivePlayer.selectedVirtue.Count}.");

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
        Player currentPlayer = TurnManager.Instance.ActivePlayer;
        if (TurnManager.Instance.ActivePlayer == null) return;


        if (TurnManager.Instance.ActivePlayer.selectedVirtue.Contains(virtueUIre))
        {
            TurnManager.Instance.ActivePlayer.selectedVirtue.Remove(virtueUIre);
            Debug.Log($"Virtue {virtueUIre.name} removed from selected virtues.");
        }
        else
        {
            Debug.Log($"Cannot remove {virtueUIre.name} as it is not in selected virtues.");
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
