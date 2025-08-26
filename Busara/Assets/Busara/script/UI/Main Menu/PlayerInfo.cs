using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "playerInfo", fileName = "New Player Info")]
public class PlayerInfo : ScriptableObject
{
   public List<PlayerData> Players = new List<PlayerData>();

}
[System.Serializable]
public struct PlayerData
{
    public int PlayerID;
    public string PlayerName;
    public Kingdom Kingdom;
    public PlayerData(int playerID, string playerName, Kingdom kingdom)
    {
        PlayerID = playerID;
        PlayerName = playerName;
        Kingdom = kingdom;
    }
}
