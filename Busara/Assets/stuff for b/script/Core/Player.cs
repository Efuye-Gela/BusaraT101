using UnityEngine;
using System.Collections.Generic;

public class Player : MonoBehaviour
{
    public string Name;
    public List<Virtue> Virtues;
    public List<Resource> selectedResources;
    public List<Player> selectedPlayers;
    public Board Board;
    public Kingdom Kingdom;
}
