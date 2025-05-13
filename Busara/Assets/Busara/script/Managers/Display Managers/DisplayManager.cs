using NUnit.Framework;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DisplayManager : Manager<DisplayManager>
{
    public GameObject PopUpInfoPanel;
    public TMP_Text consoleText;
    public TMP_Text PopUpText;

   /*[Kingdom display]*/

    public TMP_Text kingdomName;
    public TMP_Text kingdomDescription;
    public VirtueImages[] VirtueImages;

    /*Power display*/
    public TMP_Text PowerName;
    public TMP_Text PowerDescription;
    public List<PlayerRepresentation> ThePlayersList = new List<PlayerRepresentation>();
    public Transform SpawnArea;

    /* Player information area */
    public TMP_Text[] playerName;


    private void OnEnable()
    {
        Application.logMessageReceived += LogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= LogMessage;
    }
    private void Update()
    {
        PowerInfoDisplay();
        KingdomInfoDisplay();
    }

    /* Display consile info */
    private void LogMessage(string logString, string stackTrace, LogType type)
    {
        if (consoleText != null)
        {
            consoleText.text = logString;
        }
    }

    /* Display special info Pop Up */
    public void Communication(string info)
    {
        if (PopUpText != null)
        {
            PopUpInfoPanel.gameObject.SetActive(true);
            PopUpText.text = info;
        }
    }
    /*Toggling the Left side UI*/
    public void UIToggle(GameObject gameObject) 
    {
        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    /*Kingdom info display*/
    public void KingdomInfoDisplay()
    {
        Player ThePlayer = TurnManager.Instance.ActivePlayer;
        kingdomName.text = ThePlayer.Kingdom.kingdomName;
        kingdomDescription.text = ThePlayer.Kingdom.kingdomStory;
        virtueAssigner();
    }
    /*virtue assignment*/
    public void virtueAssigner()
    {
        Player ThePlayer = TurnManager.Instance.ActivePlayer;
        VirtueImages[0].virtues.sprite =  ThePlayer.Kingdom.virtuesForWin[0].virtues.virtueIcon;
        VirtueImages[0].NumberOfVirtues.text = "X" + ThePlayer.Kingdom.virtuesForWin[0].NumberofVirtues.ToString();

        VirtueImages[1].virtues.sprite = ThePlayer.Kingdom.virtuesForWin[1].virtues.virtueIcon;
        VirtueImages[1].NumberOfVirtues.text = "X" + ThePlayer.Kingdom.virtuesForWin[1].NumberofVirtues.ToString();

        VirtueImages[2].virtues.sprite = ThePlayer.Kingdom.virtuesForWin[2].virtues.virtueIcon;
        VirtueImages[2].NumberOfVirtues.text = "X" + ThePlayer.Kingdom.virtuesForWin[2].NumberofVirtues.ToString();
    }

    /*Power info Display*/
    public void PowerInfoDisplay()
    {
        Player ThePlayer = TurnManager.Instance.ActivePlayer;
        if (ThePlayer.Kingdom.power != null)
        {
            PowerName.text = ThePlayer.Kingdom.power.powerName;
            PowerDescription.text = ThePlayer.Kingdom.power.powerDescription;
            PlayerListInfo();
        }
    }

    /*Player list for the power card info list*/
    public void PlayerListInfo()
    {
        if (SpawnArea == null)
            return;
        //list of players
        List <Player> players = PlayerManager.Instance.Players;

        foreach(Player player in players)
        {

            ThePlayersList[player.PlayerNumber].player = players[player.PlayerNumber];
            ThePlayersList[player.PlayerNumber].PlayerName.text = players[player.PlayerNumber].Name;
            if (player == TurnManager.Instance.ActivePlayer)
            {
                ThePlayersList[player.PlayerNumber].gameObject.SetActive(false);
            }
            if (player != TurnManager.Instance.ActivePlayer)
            {
                ThePlayersList[player.PlayerNumber].gameObject.SetActive(true);
            }
        }
        /*Player one*//*
        ThePlayersList[0].player = players[0];
        ThePlayersList[0].PlayerName.text = players[0].Name;
        *//*Player two*//*
        ThePlayersList[1].player = players[1];
        ThePlayersList[1].PlayerName.text = players[1].Name;
        *//*Player three*//*
        ThePlayersList[2].player = players[2];
        ThePlayersList[2].PlayerName.text = players[2].Name;
        *//*Player four*//*
        ThePlayersList[3].player = players[3];
        ThePlayersList[3].PlayerName.text = players[3].Name;*/

    }

    /*Display on and off the increase and Decrease*/
    public void TOnIncDec()
    {
        foreach (VirtueUI VUI in TurnManager.Instance.ActivePlayer.state.VirtueUIList)
        {
            VUI.instance.TurnOnINCDEC();
        }
    }
    public void TOffIncDec()
    {
        foreach (VirtueUI VUI in TurnManager.Instance.ActivePlayer.state.VirtueUIList)
        {
            VUI.instance.TurnOffINCDEC();
        }
    }
}

public class VirtueImages
{
    public Image virtues;
    public TMP_Text NumberOfVirtues;
}
