using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerStatDisplay : Manager<PlayerStatDisplay>
{
    public PlayerState PlayerStat;
    public List<PlayerState> PlayersStats;

    public Transform StatParent;
    public List<PlayerState> TheState;

    public TMP_Text consoleText;
    public TMP_Text infoText;

    private void OnEnable()
    {
        Application.logMessageReceived += LogMessage;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= LogMessage;
    }

    private void LogMessage(string logString, string stackTrace, LogType type)
    {
        if (consoleText != null)
        {
            consoleText.text = logString;
        }
    }
    public void generatePlayer()
    {
        Debug.Log("your stat my lord ");
    }

    public void toggleStatPanel(GameObject statPanel)
    {
        if (!statPanel.activeSelf)
        {
            statPanel.SetActive(true);
        }
        else
        {
            statPanel.SetActive(false);
        }
    }

    public void Communication(string info)
    {
        if(infoText != null)
        {
            infoText.text = info;
        }
    }
    void Update()
    {
        ProfilePanel();
    }

    public void ProfilePanel()
    {
        foreach(PlayerState player in PlayersStats)
        {
            if(player.player == TurnManager.Instance.ActivePlayer)
            {
                player.gameObject.SetActive(true);
            }
            else
            {
                player.gameObject.SetActive(false);
            }
        }
    }
}
