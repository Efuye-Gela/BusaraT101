using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public enum state
{
    start, playerOne, playerTwo, playerThree, playerFour, none
}

public class MainManager : MonoBehaviour
{
    craftingManager craftingManager;
    deckManager deckManager;
    SelectedItem selectedItem;

    public TMP_Text printText;
    public TMP_InputField numberOfplayer;

    int NOP;
    string numb;

    private void Start()
    {
        deckManager = GetComponent<deckManager>();
        craftingManager = GetComponent<craftingManager>();

        Application.logMessageReceived += HandleLog;
/*
        numberOfplayer.contentType = TMP_InputField.ContentType.IntegerNumber;
        numb = numberOfplayer.text;*/
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        printText.text = logString;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }


    public void checkForNumberOfPlayer()
    {
        //NOP = numb.ToInt();
    }
    public void Quit()
    {
        Application.Quit();
    }
    public void loadManager(int index)
    {
        SceneManager.LoadScene(index);
    }

}
