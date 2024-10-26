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
    Movingpeice mpc;

    public TMP_Text printText;
    public TMP_InputField numberOfplayer;

    int NOP;
    string numb;

    private void Start()
    {
        deckManager = GetComponent<deckManager>();
        craftingManager = GetComponent<craftingManager>();
        mpc = GetComponent<Movingpeice>();
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
    public void DeactivateAllSlotsIN4()
    {
        foreach (slotExtra slot in mpc.placeSlotsP4)
        {
            if (slot != null && slot.gameObject != null) 
            {
                slot.gameObject.SetActive(false); 
            }
        }
    }

}
