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
      
    }


}
