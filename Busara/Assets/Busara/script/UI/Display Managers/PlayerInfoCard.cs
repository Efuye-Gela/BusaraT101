using Sirenix.Serialization;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerInfoCard : MonoBehaviour, TurnManager.TurnBeginListener
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
        if (player != null && VirtuesList != null && VirtueUIList != null)
            VirtueDisplay();
        HardWinterDisaster.OnDiscardStateChanged += GetPlayerVirtueCount;
        if (TurnManager.Instance != null)
            TurnManager.Instance.AddTurnBeginListeners(this);
        if (PowerManager.Instance != null)
            PowerManager.Instance.OnStateChanged += GetPlayerVirtueCount;
        GetPlayerVirtueCount();
    }

    private void OnDisable()
    {
        HardWinterDisaster.OnDiscardStateChanged -= GetPlayerVirtueCount;
        if (TurnManager.Instance != null)
            TurnManager.Instance.RemoveTurnBeginListener(this);
        if (PowerManager.Instance != null)
            PowerManager.Instance.OnStateChanged -= GetPlayerVirtueCount;
    }

    public void OnTurnBegin()
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

        if (PlayerName != null)
            PlayerName.text = player.Name + (HardWinterDisaster.Active != null &&
                TurnManager.Instance.ActivePlayer == player && HardWinterDisaster.Active.RequiresDiscard(player)
                ? " - discard 1 virtue" : "");

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
                Player viewer = HardWinterDisaster.Active != null ? TurnManager.Instance.ActivePlayer :
                    PowerManager.Instance != null ? PowerManager.Instance.Viewer :
                    TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : null;
                virtueUI.NumberOfvirtues.text = player.CanSeeVirtues(viewer) ? virtueCounts[virtueType].ToString() : "?";
                virtueUI.currentPlayer = player;
                virtueUI.SetDisasterDiscard(HardWinterDisaster.Active != null &&
                    HardWinterDisaster.Active.CanDiscard(player, virtueUI.virtueType));
            }
        }
    }
}
