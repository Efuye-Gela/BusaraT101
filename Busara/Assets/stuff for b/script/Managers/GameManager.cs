using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Manager<GameManager>
{
    public bool multidraw = false;
    public Player lastDrawnPlayer = null;
    public bool isTournament = false;

    public bool IsSpecialTurn = false;

    public Player currentPlayer;

    private void Awake()
    {
        currentPlayer = TurnManager.Instance.ActivePlayer;
    }
    void Start()
    {
        TurnManager.Instance.OnSpecialCardDrawnEvent += HandleSpacaileTurn; 
    }
    private void Update()
    {
        currentPlayer = TurnManager.Instance.ActivePlayer;
    }
    private void HandleSpacaileTurn()
    {
        IsSpecialTurn = true;
    }

    public void OnTapRemove()
    {
        if (IsSpecialTurn)
        {
            List<Resource> resources = TurnManager.Instance.ActivePlayer.selectedResources;
            if (resources != null && resources.Count > 0)
            {
                if (resources.Count > 1)
                    PlayerStatDisplay.Instance.Communication("Please select one resource only");
                else
                {
                    resources[0].slot.EmptySlot();
                    TurnManager.Instance.ActivePlayer.selectedResources.Clear();
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                }
            }
        }
        else
        {
            Debug.Log("only on specail turn");
        }
    
    }

    public void OnPowerUser(Player player)
    {
        if(TurnManager.Instance.ActivePlayer.Kingdom.power != null)
        {
            if(TurnManager.Instance.ActivePlayer == player)
                TurnManager.Instance.ActivePlayer.Kingdom.power.Execute();
        }
        if (TurnManager.Instance.ActivePlayer != player)
        {
            TurnManager.Instance.ActivePlayer.selectedPlayer = player;
            Debug.Log($"{player.Name} touched.");
        }

    }

    public void setPlayer()
    {
        if(currentPlayer == null)
        {
            currentPlayer = TurnManager.Instance.ActivePlayer;
        }
    }

    public void Quit()
    {
        Application.Quit();
    }
    public void Load(int index)
    {
      SceneManager.LoadScene(index);
    }
}
