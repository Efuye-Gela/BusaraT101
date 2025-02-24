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
    public List<Virtue> selectedVirtues = new List<Virtue>();
    public List<Player> selectedPlayers = new List<Player>();
    public  bool hasDrawnResource = false;

    [Space]
    public Board Board;
    public Kingdom Kingdom;
}
