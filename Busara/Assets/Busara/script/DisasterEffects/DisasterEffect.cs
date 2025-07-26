using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class DisasterEffect : MonoBehaviour
{
    [TextArea]
    public string description;

    public abstract List<Player> GetAffectedPlayers();
    public abstract void Execute(List<Player> affectedPlayers, Action onDisasterComplete);
}
    