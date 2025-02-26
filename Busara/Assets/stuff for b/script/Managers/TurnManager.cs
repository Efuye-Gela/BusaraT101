using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

public class TurnManager : Manager<TurnManager>
{

    List<TurnBeginListener> turnBeginListeners = new List<TurnBeginListener>();

    public void AddTurnBeginListeners(TurnBeginListener listener)
    {
        if (!turnBeginListeners.Contains(listener))
            turnBeginListeners.Add(listener);
    }

    public void RemoveTurnBeginListener(TurnBeginListener listener)
    {
        if (turnBeginListeners.Contains(listener))
            turnBeginListeners.Remove(listener);
    }

    public void TriggerTurnBeginListeners()
    {
        foreach (TurnBeginListener listener in turnBeginListeners.ToArray())
        {
            listener.OnTurnBegin();
        }
    }

    public interface TurnBeginListener
    {
        void OnTurnBegin();
        
    }
    List<TurnEndListener> turnEndListeners = new List<TurnEndListener>();

    public void AddTurnEndListeners(TurnEndListener listener)
    {
        if (!turnEndListeners.Contains(listener))
            turnEndListeners.Add(listener);
    }

    public void RemoveTurnEndListener(TurnEndListener listener)
    {
        if (turnEndListeners.Contains(listener))
            turnEndListeners.Remove(listener);
    }

    public void TriggerTurnEndListeners()
    {
        foreach (TurnEndListener listener in turnEndListeners.ToArray())
        {
            listener.OnTurnEnd();
        }
    }
    public interface TurnEndListener
    {
        void OnTurnEnd();

    }



    public Player firstPlayer;

    private Player activePlayer;
    public Player ActivePlayer => activePlayer;

    //public Action<Player> onTurn;

    public bool isSpecialCardDrawn = false; // Flag for special card state
    public bool returnToFirstPlayer = false; // 
    private List<Player> specialActionList; //
    Player lastSequentialPlayer = null;

    public GameObject WonScreen;
    public event Action OnSpecialCardDrawnEvent;

    void Start()
    {
        StartTurns();
    }

    public void StartTurns()
    {
        BeginTurn(firstPlayer);
    }

    void BeginTurn(Player player)
    {
        activePlayer = player;
        Debug.Log("Current Turn : " + player.name +" Board:"+ player.Board.name);
        //onTurn?.Invoke(player);
        TriggerTurnBeginListeners();
    }

    public bool HasTurn(Player player)
    {
        return activePlayer == player;
    }

    public void CompleteTurn(Player player, Action onSpecialActionComplete = null)
    {
        if (player != activePlayer)
            return;

        if (isSpecialCardDrawn)
        {
            Player nextSpecialPlayer = null;
            Debug.Log("Special Turn Ended for " + player.name);
            if (specialActionList.Count > 1)
            {
                specialActionList.Remove(player);
                nextSpecialPlayer = specialActionList[0];
                EndTurn(activePlayer);
                BeginTurn(nextSpecialPlayer);
            }
            else if (specialActionList.Count == 1)
            {
                specialActionList.Remove(player);
                
                Debug.Log("Last Special Turn Ended");
                isSpecialCardDrawn = false;
                EndTurn(player);
                if (returnToFirstPlayer)
                { 
                    returnToFirstPlayer = false;
                    BeginTurn(lastSequentialPlayer);
                }
                else
                { 
                    activePlayer = lastSequentialPlayer;
                    Player nextNormalPlayer = GetNextPlayer();
                    BeginTurn(nextNormalPlayer);
                }

                // Execute the passed action if provided
                onSpecialActionComplete?.Invoke();
            }
            return;
        }

        Player nextPlayer = GetNextPlayer();
        TriggerTurnEndListeners();
        EndTurn(activePlayer);
        Debug.Log("Turn Ended");
        BeginTurn(nextPlayer);
    }

    Player GetNextPlayer()
    {
        return PlayerManager.Instance.GetNextPlayer(activePlayer);
    }

    void EndTurn(Player player)
    {
        SelectionManager.Instance.UnhighlightAll();
        WinconditionMeet();
        activePlayer = null;
    }

    // Triggered when a special card is drawn
    public void OnSpecialCardDrawn(bool _returnToFirstPlayer,List<Player> players)
    {
        lastSequentialPlayer = activePlayer;
        returnToFirstPlayer = _returnToFirstPlayer;
        Debug.Log("Special Action to be Performed.");
        isSpecialCardDrawn = true;
        specialActionList = players;
        Player nextPlayer = players[0];
        EndTurn(activePlayer);
        BeginTurn(nextPlayer);
        Debug.Log("Special Turn for "+ nextPlayer.name);
        OnSpecialCardDrawnEvent?.Invoke();
    }
    public void WinconditionMeet()
    {
        if (ActivePlayer.Kingdom == null || ActivePlayer.Virtues == null)
        {
            Debug.Log("No Kingdom assigned or Virtues list is empty.");
            return;
        }

        Dictionary<VirtueType, int> playerVirtueCounts = new Dictionary<VirtueType, int>();


        foreach (VirtueType type in System.Enum.GetValues(typeof(VirtueType)))
        {
            playerVirtueCounts[type] = 0;
        }
        foreach (Virtue virtue in ActivePlayer.Virtues)
        {
            if (virtue != null)
            {
                playerVirtueCounts[virtue.type]++;
            }
        }

        foreach (Kingdom.VirtuesForCost requirement in ActivePlayer.Kingdom.virtuesForWin)
        {
            if (requirement == null || requirement.virtues == null)
                continue;

            VirtueType requiredType = requirement.virtues.type;
            int requiredCount = requirement.NumberofVirtues;

            if (!playerVirtueCounts.ContainsKey(requiredType) || playerVirtueCounts[requiredType] < requiredCount)
            {
                return;
            }
        }

        Debug.Log(ActivePlayer.Name + " has met the win conditions!");
        WonScreen.SetActive(true);
    }



}
