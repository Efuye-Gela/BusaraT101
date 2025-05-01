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
    public PlayerRepresentation ThePlayer;
    public List<PlayerRepresentation> ThePlayersList = new List<PlayerRepresentation>();
    public Transform SpawnArea;

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
        if (ThePlayer != null)
        {
            PowerInfoDisplay();
            KingdomInfoDisplay();
        }
    }

    private void LogMessage(string logString, string stackTrace, LogType type)
    {
        if (consoleText != null)
        {
            consoleText.text = logString;
        }
    }

    /* Display special Pop Up */
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
        if(ThePlayer == null || SpawnArea == null)
            return;

        /*Make sure that there is no representation of players*/
        if(ThePlayersList.Count > 0)
        {
            foreach (PlayerRepresentation playerRep in ThePlayersList)
            {
                if(playerRep != null)
                    Destroy(playerRep.gameObject);
            }
        }
   

        foreach (Player player in  PlayerManager.Instance.Players) 
        {
            PlayerRepresentation PlayerRep = Instantiate(ThePlayer, SpawnArea.transform);
            PlayerRep.player = player;
            PlayerRep.PlayerName.text = player.Name;
            ThePlayersList.Add(PlayerRep);
        }
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
