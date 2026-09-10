using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class Player : MonoBehaviour
{
    public string Name;
    public bool IsBotControlled;
    public List<Virtue> Virtues = new List<Virtue>();
    public int PlayerNumber;

    [Space]
    [Header("Selections")]
    public List<Resource> selectedResources = new List<Resource>();
    public List<Slot> selectedSlots = new List<Slot>();
    public List<Virtue> selectedVirtue = new List<Virtue>();
    public Player selectedPlayer;
    public List<Player> selectedPlayers = new List<Player>();
    public bool hasDrawnResource = false;
    public bool hasFinishedSettingUp = false;
    public SetupCard setUpCard;

    [Space]
    public Board Board;
    public Kingdom Kingdom;
    public PlayerState state;
    public bool kingdomRevealed;
    public bool virtuesHidden;
    public readonly HashSet<Kingdom> knownKingdoms = new HashSet<Kingdom>();

    public bool CanSeeVirtues(Player viewer)
    {
        return !virtuesHidden || viewer == this;
    }

    public bool CanSeeKingdom(Player viewer)
    {
        return kingdomRevealed || viewer == this ||
            (viewer != null && viewer.knownKingdoms.Contains(Kingdom));
    }
}
