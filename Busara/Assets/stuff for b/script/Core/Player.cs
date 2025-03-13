using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class Player : MonoBehaviour
{
    public string Name;
    public List<Virtue> Virtues;

    [Space]
    [Header("Selections")]
    public List<Resource> selectedResources = new List<Resource>();
    public List<Slot> selectedSlots = new List<Slot>();
    public List<Virtue> selectedVirtue = new List<Virtue>();
    public Player selectedPlayer;
    public List<Player> selectedPlayers = new List<Player>();
    public bool hasDrawnResource = false;
    public List<ResourceType> resourceTypeCollection= new List<ResourceType>();

    [Space]
    public Board Board;
    public Kingdom Kingdom;
    public PlayerState state;
}
