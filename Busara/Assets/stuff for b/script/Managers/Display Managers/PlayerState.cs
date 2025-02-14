using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NUnit.Framework;
using Unity.VisualScripting;
using System.Linq;

public class PlayerState : MonoBehaviour
{
    public Player player;
    public TMP_Text PlayerName;
    public VirtueUI VirtuePrefab;
    public Transform SpawnArea;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    public static PlayerState instance;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        PlayerName.text = player.Name;
        VirtueDisplay();
        GetPlayerVirtueCount();
    }
    public void Update()
    {
        GetPlayerVirtueCount();
    }
    public void VirtueDisplay()
    {
        foreach (Virtue virtue in VirtuesList)
        {
            string VName = virtue.name;
           // Debug.Log(VName);
            VirtueUI VerNew = Instantiate(VirtuePrefab, SpawnArea);
            if(VerNew != null && VName != null)
            {
                VerNew.virtueUIre = virtue;
                VerNew.VirtueName.text = VName;
                VirtueUIList.Add(VerNew);
            } 
        }
    }

    public void GetPlayerVirtueCount()
    {
        if (player == null || player.Virtues == null || VirtueUIList == null)
            return;

        Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();

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
                virtueUI.NumberOFvirtues.text = virtueCounts[virtueType].ToString();
            }
        }
    }



}
