using NUnit.Framework;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class DisplayManager : Manager<DisplayManager>, TurnManager.TurnBeginListener
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
    public List<GameObject> UIComponentList;
    private void Start()
    {
       // TurnManager.Instance.AddTurnEndListeners(this);
        TurnManager.Instance.AddTurnBeginListeners(this);
    }

    /* Display  info */
    public IEnumerator Massage(string logString)
    {
        consoleText.gameObject.SetActive(true);

        if (consoleText != null)
        {
            consoleText.text = logString;
        }

        // Reset full opacity
        Color originalColor = consoleText.color;
        originalColor.a = 1f;
        consoleText.color = originalColor;

        // Wait before fading
        yield return new WaitForSeconds(0.5f);

        // Fade Out
        float fadeDuration = 0.75f;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            Color newColor = consoleText.color;
            newColor.a = Mathf.Lerp(1f, 0f, t);
            consoleText.color = newColor;
            elapsed += Time.deltaTime;
            yield return null;
        }
            
        // Ensure fully transparent
        Color finalColor = consoleText.color;
        finalColor.a = 0f;
        consoleText.color = finalColor;

        consoleText.gameObject.SetActive(false);
    }

    /* Display special info Pop Up */
    public void ErrorMassage(string info)
    {
        if (PopUpText != null)
        {
            PopUpInfoPanel.gameObject.SetActive(true);
            PopUpText.text = info;
        }
    }
    /*Toggling the Left side UI*/

    /*Kingdom info display*/
    public void KingdomInfoDisplay()
    {
        Player ThePlayer = TurnManager.Instance.ActivePlayer;
        kingdomName.text = ThePlayer.Kingdom.kingdomName;
        kingdomDescription.text = ThePlayer.Kingdom.kingdomStory;
        virtueAssigner();
    }
    /*virtue assignment for Kingdom*/
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
    /*UI component Display*/
    public void UIToggleDisplayer(GameObject gameObject)
    {
        foreach(GameObject UI in UIComponentList)
        {
            if(UI == gameObject)
            {
                gameObject.SetActive(true);
            }
            else
            {
                UI.SetActive(false);
            }
        }
    }

    public void OnTurnBegin()
    {
        KingdomInfoDisplay();
        StartCoroutine(Massage("Current Turn : " + TurnManager.Instance.ActivePlayer.Name));
    }


}

public class VirtueImages
{
    public Image virtues;
    public TMP_Text NumberOfVirtues;
}
