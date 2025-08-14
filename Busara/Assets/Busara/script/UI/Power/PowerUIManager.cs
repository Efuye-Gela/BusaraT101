using Sirenix.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PowerUIManager : Manager<PowerUIManager>, TurnManager.TurnBeginListener
{
    private Player player;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    [OdinSerialize]
    public Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();
    public TMP_Text powerInfo;
    public List<Transform> PowerPanelComponents;

    private void OnEnable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnBeginListeners(this);
    }
    private void OnDisable()
    {
        if (TurnManager.Instance != null)
            TurnManager.Instance.RemoveTurnBeginListener(this);
    }

    private void Start()
    {
        if (TurnManager.Instance.ActivePlayer != null)
        {
            player = TurnManager.Instance.ActivePlayer;
        }
        else
        {
            Debug.Log("No name");
        }
        PowerManager.Instance.OnPowerActivated += PowerActivated;
        PowerManager.Instance.OnPowerDeactivated += PowerDeactivated;
    }

    private void PowerActivated()
    {
        VirtueDisplay();
        GetPlayerVirtueCount();
        PowerPanelLayout();
        PowerMassage($"Please select virtue to use your power");
    }

    private void PowerDeactivated()
    {
        PowerPanelComponents[0].gameObject.SetActive(false);
    }
    public void PowerPanelLayout()
    {
        PowerPanelComponents[0].gameObject.SetActive(true);
        PowerPanelComponents[1].gameObject.SetActive(true);
        PowerPanelComponents[2].gameObject.SetActive(false);
    }
    public void VirtueDisplay()
    {
        for (int i = 0; i < VirtuesList.Count; i++)
        {
            VirtueUIList[i].virtueType = VirtuesList[i];
            VirtueUIList[i].VirtueName.text = VirtuesList[i].name;
            VirtueUIList[i].virtueImage.sprite = VirtuesList[i].virtueIcon;
            VirtueUIList[i].currentPlayer = player;
        }
    }
    public void GetPlayerVirtueCount()
    {
        if (player == null || player.Virtues == null || VirtueUIList == null)
            return;

        foreach (VirtueType type in System.Enum.GetValues(typeof(VirtueType)))
        {
            virtueCounts[type] = 0;
        }

        foreach (Virtue virtue in player.Virtues)
        {
            if (virtueCounts.ContainsKey(virtue.type))
            {
                virtueCounts[virtue.type]++;
            }
        }

        foreach (VirtueUI virtueUI in VirtueUIList)
        {
            if (virtueUI != null && virtueUI.virtueType != null)
            {
                VirtueType virtueType = virtueUI.virtueType.type;
                virtueUI.NumberOfvirtues.text = virtueCounts[virtueType].ToString();
            }
        }
    }
    public void OnTurnBegin()
    {
        if (TurnManager.Instance.ActivePlayer != null)
        {
            player = TurnManager.Instance.ActivePlayer;
        }
        GetPlayerVirtueCount();
    }
    public void PowerMassage(string logString)
    {
        powerInfo.gameObject.SetActive(true);
        if (powerInfo != null)
        {
            powerInfo.text = logString;
        }


    }
}
