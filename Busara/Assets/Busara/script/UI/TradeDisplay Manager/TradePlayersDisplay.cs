using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TradePlayersDisplay : MonoBehaviour
{
    [SerializeField] private List<Button> playerButtons;

    public void DisplayTradeStatus()
    {
        List<Player> playerList = new List<Player>(TradeManager.Instance.acceptedPlayers);
        List<Player> rejectedPlayers = new List<Player>(TradeManager.Instance.rejectedPlayers);
        playerList.AddRange(rejectedPlayers);

        for (int i = 0; i < playerButtons.Count; i++)
        {
            if (i < playerList.Count)
            {
                Player player = playerList[i];
                Button button = playerButtons[i];
                button.gameObject.GetComponentInChildren<TMP_Text>().text = player.name;
                button.onClick.RemoveAllListeners();

                if (TradeManager.Instance.acceptedPlayers.Contains(player))
                {
                    button.gameObject.GetComponent<Image>().color = Color.green;
                    button.onClick.AddListener(() =>
                    {
                        TradeManager.Instance.PlayerSelectsTradePartner(player);
                        button.gameObject.GetComponent<Image>().color = Color.yellow;
                    });
                    button.interactable = true;
                }
                else
                {
                    button.gameObject.GetComponent<Image>().color = Color.red;
                    button.interactable = false;
                }

            }
            else
            {
                playerButtons[i].gameObject.SetActive(false);
            }
        }
    }

    public void OnTapConfirmTrade()
    {

        TradeManager.Instance.TradeResources();
    }


}