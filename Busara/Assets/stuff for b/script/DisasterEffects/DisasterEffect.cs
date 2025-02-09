using UnityEngine;
using System.Collections.Generic;

public abstract class DisasterEffect : MonoBehaviour
{
    
    public abstract (bool, List<Player>) IsValid(List<Player> allPlayers);
    public abstract void Execute();
}
