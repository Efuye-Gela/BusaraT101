using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerStatDisplay : MonoBehaviour
{
    public PlayerState PlayerStat;
    public Transform StatParent;
    public List<PlayerState> TheState;

    public TMP_Text consoleText;

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

    public void togelStatPanel(GameObject statPanal)
    {
        if (!statPanal.activeSelf)
        {
            statPanal.SetActive(true);
        }
        else
        {
            statPanal.SetActive(false);
        }
    }
}
