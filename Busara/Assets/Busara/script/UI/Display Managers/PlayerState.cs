using System.Collections.Generic;
using UnityEngine;
using TMPro;
using NUnit.Framework;
using Unity.VisualScripting;
using System.Linq;
using Sirenix.Serialization;
using Sirenix.OdinInspector;

[System.Serializable]
public class PlayerState : SerializedMonoBehaviour, ForgeManager.IForgeListener, TurnManager.TurnBeginListener
{
    private Player player;
    public TMP_Text PlayerName;
    public VirtueUI VirtuePrefab;
    public Transform SpawnArea;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    [OdinSerialize]
    public Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();

    /*Player info spawn */
    public PlayerInfoCard playerInfoCard;
    public List<PlayerInfoCard> playerInfoCardList = new List<PlayerInfoCard>();
    public Transform InfoCardSpawnArea;

    private void OnEnable()
    {
        if (ForgeManager.Instance != null)
            ForgeManager.Instance.RegisterForgeListener(this);
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnBeginListeners(this);
    }
    private void OnDisable()
    {
        if (ForgeManager.Instance != null)
            ForgeManager.Instance.UnregisterForgeListener(this);
        if (TurnManager.Instance != null)
            TurnManager.Instance.RemoveTurnBeginListener(this);
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
    }
    public void VirtueDisplay()
    {
        for(int i = 0; i < VirtuesList.Count; i++)
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
        if (player != null)
        {
            PlayerName.text = player.Name;
        }
        GetPlayerVirtueCount();
    }
    public void OnForgeCompleted(List<Virtue> forgedVirtues, List<Resource> usedResources)
    {
        GetPlayerVirtueCount();
    }


}
