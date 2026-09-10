using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;

[DefaultExecutionOrder(200)]
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


    List<ISpecialTurnEndListeners> SpecialTurnEndListeners = new List<ISpecialTurnEndListeners>();

    public void AddSpecialTurnEndListeners(ISpecialTurnEndListeners listener)
    {
        if (!SpecialTurnEndListeners.Contains(listener))
            SpecialTurnEndListeners.Add(listener);
    }

    public void RemoveSpecialTurnEndListeners(ISpecialTurnEndListeners listener)
    {
        if (SpecialTurnEndListeners.Contains(listener))
            SpecialTurnEndListeners.Remove(listener);
    }

    public void TriggerSpecialTurnEndListeners()
    {
        foreach (ISpecialTurnEndListeners listener in SpecialTurnEndListeners.ToArray())
        {
            listener.OnSpecialTurnEnd();
        }
    }
    public interface ISpecialTurnEndListeners
    {
        void OnSpecialTurnEnd();

    }


    public Player firstPlayer;

    private Player activePlayer;
    public Player ActivePlayer => activePlayer;

    public bool isSpecialCardDrawn = false;
    public bool returnToFirstPlayer = false;
    private List<Player> specialActionList;
    Player lastSequentialPlayer = null;
    private Action specialCompletion;
    public bool TurnsStarted { get; private set; }

    

    void Start()
    {
        if (PlayerManager.Instance == null || !PlayerManager.Instance.IsAwaitingSetup)
            StartTurns();
    }

    public void StartTurns()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.IsAwaitingSetup)
        {
            Debug.LogWarning("Finish player setup before starting the game.");
            return;
        }
        if (TurnsStarted)
            return;
        if (firstPlayer == null || firstPlayer.Board == null ||
            PlayerManager.Instance == null || !PlayerManager.Instance.Players.Contains(firstPlayer))
        {
            Debug.LogError("Choose a participating first player with a board before starting turns.");
            return;
        }
        TurnsStarted = true;
        BeginTurn(firstPlayer);
    }

    void BeginTurn(Player player, bool newNormalTurn = true)
    {

        activePlayer = player;
        Debug.Log("Current Turn : " + player.name + " Board:" + player.Board.name);
        TriggerTurnBeginListeners(); 
        if (newNormalTurn && activePlayer == player && !isSpecialCardDrawn &&
            player.hasFinishedSettingUp && PowerManager.Instance != null)
            PowerManager.Instance.BeginNormalTurn(player);

    }
    Player GetNextPlayer()
    {
        return PlayerManager.Instance.GetNextPlayer(activePlayer);
    }

    void EndTurn(Player player)
    {
        SelectionManager.Instance.UnhighlightAll();
        activePlayer = null;
    }

    public bool HasTurn(Player player)
    {
        return activePlayer == player;
    }


    // Triggered when a special card is drawn
    public void OnSpecialTurn(bool _returnToFirstPlayer, List<Player> players, Action onComplete = null)
    {
        if (players == null || players.Count == 0 || isSpecialCardDrawn)
        {
            Debug.LogError("A special turn needs participants and cannot overlap another special turn.");
            return;
        }
        lastSequentialPlayer = activePlayer;
        returnToFirstPlayer = _returnToFirstPlayer;
        Debug.Log("Special Action to be Performed.");
        isSpecialCardDrawn = true;
        specialActionList = new List<Player>(players);
        specialCompletion = onComplete;
        Player nextPlayer = players[0];
        EndTurn(activePlayer);
        BeginTurn(nextPlayer, false);
        Debug.Log("Special Turn for " + nextPlayer.name);
    }
    public void CompleteTurn(Player player)
    {
        if (player != activePlayer)
            return;
        if (isSpecialCardDrawn)
        {
            CompleteSpecialTurn(player);
            return;
        }
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
        {
            Debug.LogWarning("Finish the pending power or reaction before ending the turn.");
            return;
        }
        if (player.hasFinishedSettingUp && PowerManager.Instance != null)
            PowerManager.Instance.CompleteAction(player, () => FinishNormalTurn(player));
        else
            FinishNormalTurn(player);
    }

    private void FinishNormalTurn(Player player)
    {
        Player nextPlayer = GetNextPlayer();
        if (PowerManager.Instance != null)
            nextPlayer = PowerManager.Instance.NextPlayer(nextPlayer);
        TriggerTurnEndListeners();
        player.hasDrawnResource = false;
        EndTurn(activePlayer);
        Debug.Log("Turn Ended");
        BeginTurn(nextPlayer);
    }

    public void CompleteSpecialTurn(Player player, Action onSpecialActionComplete = null)
    {
        if (player != activePlayer)
            return;
        if (PowerManager.Instance != null && PowerManager.Instance.IsBusy)
        {
            Debug.LogWarning("Finish the pending choice before completing a special turn.");
            return;
        }

        if (isSpecialCardDrawn)
        {
            if (HardWinterDisaster.Active != null && HardWinterDisaster.Active.RequiresDiscard(player))
            {
                Debug.LogWarning("Choose a virtue to discard before completing this special turn.");
                return;
            }
            SelectionManager.Instance.OnTurnEnd();
            specialActionList.Remove(player);
            EndTurn(player);
            if (specialActionList.Count > 0)
            {
                BeginTurn(specialActionList[0], false);
                return;
            }
            bool resume = returnToFirstPlayer;
            Action callback = onSpecialActionComplete ?? specialCompletion;
            isSpecialCardDrawn = false;
            returnToFirstPlayer = false;
            specialCompletion = null;
            activePlayer = lastSequentialPlayer;
            TriggerSpecialTurnEndListeners();
            if (resume)
                BeginTurn(lastSequentialPlayer, false);
            if (callback != null)
                callback();
            else if (!resume)
                CompleteTurn(lastSequentialPlayer);
            return;
        }

        CompleteTurn(player);
    }
    public void SkipTurns(int count)
    {
        if (isSpecialCardDrawn)
        {
            Debug.LogWarning("Cannot skip turns during special card phase.");
            return;
        }

        Player playerToStartAfterSkip = activePlayer;

        for (int i = 0; i < count; i++)
        {
            playerToStartAfterSkip = PlayerManager.Instance.GetNextPlayer(playerToStartAfterSkip);
        }

        Debug.Log($"{count} turn(s) skipped. Next turn: {playerToStartAfterSkip.name}");

        // End current turn
        TriggerTurnEndListeners();
        EndTurn(activePlayer);

        // Start new turn after skipping
        BeginTurn(playerToStartAfterSkip);
    }
    public void CancelSpecialTurn()
    {
        if (!isSpecialCardDrawn)
        {
            Debug.LogWarning("No active special turn to cancel.");
            return;
        }

        Debug.Log("Special turn canceled.");

        // Cleanup special turn state
        isSpecialCardDrawn = false;
        returnToFirstPlayer = false;
        specialActionList = null;

        TriggerTurnEndListeners();
        TriggerSpecialTurnEndListeners();

        EndTurn(activePlayer);

        // Resume normal play to the player who would have gone next
        Player nextNormalPlayer = lastSequentialPlayer;
        BeginTurn(nextNormalPlayer, false);
    }


}
