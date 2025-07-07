using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NUnit.Framework;
using Unity.VisualScripting;
using System.Linq;
using Sirenix.Serialization;
using Sirenix.OdinInspector;

[System.Serializable]
public class PlayerState : SerializedMonoBehaviour
{
    private Player player;
    public TMP_Text PlayerName;
    public VirtueUI VirtuePrefab;
    public Transform SpawnArea;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    public static PlayerState instance { get; private set; }
    [OdinSerialize]
    public Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();

    /*Player info spawn */
    public PlayerInfoCard playerInfoCard;
    public List<PlayerInfoCard> playerInfoCardList = new List<PlayerInfoCard>();
    public Transform InfoCardSpawnArea;


    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (TurnManager.Instance.ActivePlayer != null)
        {
            player = TurnManager.Instance.ActivePlayer;
        }
        if (player != null)
        {
            PlayerName.text = player.Name;
        }
        else
        {
            Debug.Log("No name");
        }
        VirtueDisplay();
        GetPlayerVirtueCount();

    }
    public void Update()
    {
        if (TurnManager.Instance.ActivePlayer != null)
        {
            player = TurnManager.Instance.ActivePlayer;
        }
        if (player != null)
        {
            PlayerName.text = player.Name;
        }
        GetPlayerVirtueCount();
    }


    public void VirtueDisplay()
    {
        foreach (Virtue virtue in VirtuesList)
        {
            string VName = virtue.name;
            VirtueUI VerNew = Instantiate(VirtuePrefab, SpawnArea);
            if(VerNew != null && VName != null)
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

    public void playerInfoSpawner()
    {
        
        foreach(PlayerInfoCard PIC in playerInfoCardList)
        {
            if (PIC != null)
                Destroy(PIC.gameObject);
        }
        foreach (Player player in PlayerManager.Instance.Players)
        {
            if(player != TurnManager.Instance.ActivePlayer)
            {
                PlayerInfoCard playerInfo = Instantiate(playerInfoCard, InfoCardSpawnArea);
                playerInfo.player = player;
                playerInfoCardList.Add(playerInfo);
            }
        }

    }


}
