using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MainMenu : Manager<MainMenu>
{
    public PlayerInfo playerInfoSB;
    public List<Infoforplayer> Info = new List<Infoforplayer>();
    #region Add player and Name
    [Header("Player Info")]
    public int ID;
    public Transform SpawnRepresentation;
    public Infoforplayer PlayerRepresentationEntry;
    public TMP_InputField PlayerNameInputField;
    public Transform InputAreaPlayerInfo;
    #endregion
    #region Add Kingdom Info
    [Header("Kingdom Info")]
    public List<Kingdom> Kingdoms;
    public Kingdom ChosenKingdom;
    public Transform SpawnKingdomeArea;
    public Transform InputAreaKingdome;
    public KingdomeInfo KingdomRepresentationEntry;
    public TMP_Text KingdomName;
    #endregion
    private void OnEnable()
    {
        Infoforplayer.PlayerInputInfo += listenToPlayerEnterInfo;
        KingdomeInfo.OnKingdomeSelect += listenToKingdomeSelect;
    }
    private void OnDisable()
    {
        Infoforplayer.PlayerInputInfo -= listenToPlayerEnterInfo;
        KingdomeInfo.OnKingdomeSelect -= listenToKingdomeSelect;
    }
    
    private void Start()
    {
        SpawnKingdome();
        playerInfoSB.Players.Clear();
    }

    public void listenToKingdomeSelect(Kingdom kingdom)
    {
        ChosenKingdom = kingdom;
        KingdomName.text = ChosenKingdom.kingdomName;
    }
    public void listenToPlayerEnterInfo(int Id)
    {
        ID = Id;
        InputAreaPlayerInfo.gameObject.SetActive(true);
    }
    public void AssignPlayerNameFromInfo()
    {
        if(PlayerNameInputField.text.Length > 11)
        {
            Debug.Log("Player Name to long");
            return;
        }
        if(ChosenKingdom == null)
        {
            Debug.Log("Please choose a Kingdom");
            return;
        }
        if(PlayerNameInputField.text == "")
        {
            Info[ID].PlayerName.text = $"Player {ID}";
        }
        else
        {
            Info[ID].PlayerName.text = PlayerNameInputField.text;
            Info[ID].KingdomName.text = ChosenKingdom.kingdomName;
            Info[ID].kingdomChosen = ChosenKingdom;
        }
        InputAreaPlayerInfo.gameObject.SetActive(false);
        PlayerNameInputField.text = "";
        KingdomName.text = "Kingdom";
        ChosenKingdom = null;
    }

    public void ConformPlayerList()
    {
        if(Info.Count < 2)
        {
            Debug.Log("Need at least 2 players to start the game");
            return;
        }
        foreach (Infoforplayer info in Info)
        {
            if (info.kingdomChosen == null)
            {
                Debug.Log($"Kingdom can not be empty player{info.name}");
                return;
            }
        }
        for(int i = 0; i < Info.Count; i++)
        {
           playerInfoSB.Players.Add(new PlayerData(Info[i].PlayerID, Info[i].PlayerName.text, Info[i].kingdomChosen));   
        }
    }

    public void SpawnPlayerRepresentation()
    {
        if(Info.Count == 4)
        {
            Debug.Log("Can not have more than 4 players");
            return;
        }
        Infoforplayer representation = Instantiate(PlayerRepresentationEntry, SpawnRepresentation);
        representation.PlayerID = Info.Count;
        Info.Add(representation);
    }
    public void SpawnKingdome()
    {
        foreach(Kingdom kingdom in Kingdoms)
        {
            KingdomeInfo kingdomeInfo = Instantiate(KingdomRepresentationEntry, SpawnKingdomeArea);
            kingdomeInfo.KingdomeName.text = kingdom.kingdomName;
            kingdomeInfo.KingdomeDescriptions.text = kingdom.kingdomStory;
            kingdomeInfo.powerName.text = kingdom.power.powerName;
            kingdomeInfo.powerDescription.text = kingdom.power.powerDescription;
            kingdomeInfo.chosenKingdom = kingdom;
            for (int i = 0; i < kingdom.virtuesForWin.Length; i++)
            {
                kingdomeInfo.kingdomeVirtues[i].VirtueName.text = kingdom.virtuesForWin[i].virtues.name;
                kingdomeInfo.kingdomeVirtues[i].VirtueCount.text = "X" + kingdom.virtuesForWin[i].NumberofVirtues.ToString();
                kingdomeInfo.kingdomeVirtues[i].VirtueIcon.sprite = kingdom.virtuesForWin[i].virtues.virtueIcon;
                kingdomeInfo.kingdomeVirtues[i].resourceOneIcon.sprite = kingdom.virtuesForWin[i].virtues.ResourceOne.resourceIcon;
                kingdomeInfo.kingdomeVirtues[i].resourceTwoIcon.sprite = kingdom.virtuesForWin[i].virtues.ResourceTwo.resourceIcon;
            }
        }
    }
}
