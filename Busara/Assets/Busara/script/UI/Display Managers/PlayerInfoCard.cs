using Sirenix.Serialization;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInfoCard : MonoBehaviour
{
    public Player player;
    public TMP_Text PlayerName;
    public VirtueUI VirtuePrefab;
    public Transform SpawnArea;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    [OdinSerialize]
    public Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();

    private void OnEnable()
    {
        GetPlayerVirtueCount();
    }

    private void OnDisable()
    {
        GetPlayerVirtueCount();
    }


    private void Start()
    {
        if (player != null)
        {
            PlayerName.text = player.Name;
            VirtueDisplay();
            GetPlayerVirtueCount();
        }
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
}
