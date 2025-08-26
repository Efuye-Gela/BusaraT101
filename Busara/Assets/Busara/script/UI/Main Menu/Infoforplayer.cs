using System;
using TMPro;
using UnityEngine;

public class Infoforplayer : MonoBehaviour 
{
    public int PlayerID;
    public Kingdom kingdomChosen;
    public static Action<int> PlayerInputInfo;
    public TMP_Text PlayerName;
    public TMP_Text KingdomName;

    private void Start()
    {
        PlayerName.text = $"Player {PlayerID}";
    }
    public void PlayerInfo()
    {
        PlayerInputInfo?.Invoke(PlayerID);
    }
    public void DestroyOnceSelf()
    {
        MainMenu.Instance.Info.Remove(this);
        Destroy(gameObject);
        
    }
}
