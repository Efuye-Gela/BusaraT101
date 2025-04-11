using TMPro;
using UnityEngine;

public class DisplayManager : Manager<DisplayManager>
{
    public GameObject PopUpInfoPanel;
    public TMP_Text consoleText;
    public TMP_Text PopUpText;

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

    /* Display special Pop Up */
    public void Communication(string info)
    {
        if (PopUpText != null)
        {
            PopUpInfoPanel.gameObject.SetActive(true);
            PopUpText.text = info;
        }
    }
}
