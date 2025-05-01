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
        foreach (Virtue virtue in VirtuesList)
        {
            string VName = virtue.name;
            VirtueUI VerNew = Instantiate(VirtuePrefab, SpawnArea);
            if (VerNew != null && VName != null)
            {
                VerNew.virtueUIre = virtue;
                VerNew.VirtueName.text = VName;
                VerNew.virtueImage.sprite = virtue.virtueIcon;
                VerNew.currentPlayer = player;
                VirtueUIList.Add(VerNew);
            }
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
            if (virtueUI != null && virtueUI.virtueUIre != null)
            {
                VirtueType virtueType = virtueUI.virtueUIre.type;
                virtueUI.NumberOfvirtues.text = virtueCounts[virtueType].ToString();
            }
        }
    }
}
