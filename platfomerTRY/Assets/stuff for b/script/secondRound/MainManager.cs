using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum state
{
    start, playerOne, playerTwo, none
}

public class MainManager : MonoBehaviour
{
    craftingManager craftingManager;
    deckManager deckManager;

    public TMP_Text printText;

    private void Start()
    {
        deckManager = GetComponent<deckManager>();
        craftingManager = GetComponent<craftingManager>();

        Application.logMessageReceived += HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        printText.text = logString;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }
}
