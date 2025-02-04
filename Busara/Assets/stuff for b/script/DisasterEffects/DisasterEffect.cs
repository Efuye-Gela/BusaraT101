using UnityEngine;
using System.Collections.Generic;

public abstract class DisasterEffect : MonoBehaviour
{
    public abstract bool IsValid(Player currentPlayer, List<Player> allPlayers);
    public abstract void Execute();
}
