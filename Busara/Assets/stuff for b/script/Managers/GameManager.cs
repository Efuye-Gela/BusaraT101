using UnityEngine;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;
}
