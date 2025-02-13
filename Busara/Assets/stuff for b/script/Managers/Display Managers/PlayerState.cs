using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NUnit.Framework;
using Unity.VisualScripting;

public class PlayerState : MonoBehaviour
{
    public Player player;
    public TMP_Text PlayerName;
    public VirtueUI VirtuePrefab;
    public Transform SpawnArea;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;

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
        int count = 0;
        if (player.Virtues.Count > 0)
        {
            foreach (Virtue ver in player.Virtues)
            {
                foreach(VirtueUI Vui in VirtueUIList)
                {
                    if(ver != null && Vui != null)
                    {
                        if (Vui.virtueUIre.type == ver.type)
                        {
                            count++;
                            Vui.NumberOFvirtues.text = count.ToString();
                        }   
                    }
                    
                }
              
            }
        }

        
    }


}
