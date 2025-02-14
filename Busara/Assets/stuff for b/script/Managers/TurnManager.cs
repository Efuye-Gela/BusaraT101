using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class TurnManager : Manager<TurnManager>
{
    public Player firstPlayer;

    private Player activePlayer;
    public Player ActivePlayer => activePlayer;

    public Action<Player> onTurn;

    public bool isSpecialCardDrawn = false; // Flag for special card state
    public bool returnToFirstPlayer = false; // 
    private List<Player> specialActionList; //
    Player lastSeqentialPlayer = null;

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
        Debug.Log("Current Turn : " + player.name);
        onTurn?.Invoke(player);
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
                    BeginTurn(lastSeqentialPlayer);
                }
                else
                { 
                    activePlayer = lastSeqentialPlayer;
                    Player nextNormalPlayer = GetNextPlayer();
                    BeginTurn(nextNormalPlayer);
                }

                // Execute the passed action if provided
                onSpecialActionComplete?.Invoke();
            }
            return;
        }

        Player nextPlayer = GetNextPlayer();
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
        WinconditionMeet();
        activePlayer = null;
    }

    // Triggered when a special card is drawn
    public void OnSpecialCardDrawn(bool _returnToFirstPlayer,List<Player> players)
    {
        lastSeqentialPlayer = activePlayer;
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

        // Dictionary to store player's virtue counts by type
        Dictionary<VirtueType, int> playerVirtueCounts = new Dictionary<VirtueType, int>();

        // Initialize virtue counts
        foreach (VirtueType type in System.Enum.GetValues(typeof(VirtueType)))
        {
            playerVirtueCounts[type] = 0;
        }

        // Count the player's virtues by type
        foreach (Virtue virtue in ActivePlayer.Virtues)
        {
            if (virtue != null)
            {
                playerVirtueCounts[virtue.type]++;
            }
        }

        // Check if player meets all virtue requirements for their kingdom
        foreach (Kingdom.VirtuesForCost requirement in ActivePlayer.Kingdom.virtuesForWin)
        {
            if (requirement == null || requirement.virtues == null)
                continue;

            VirtueType requiredType = requirement.virtues.type;
            int requiredCount = requirement.NumberofVirtues;

            // If the player has fewer virtues of this type than required, they haven't won yet
            if (!playerVirtueCounts.ContainsKey(requiredType) || playerVirtueCounts[requiredType] < requiredCount)
            {
                Debug.Log("Player does not meet win conditions yet.");
                return;
            }
        }

        // If all requirements are met, player wins
        Debug.Log(ActivePlayer.Name + " has met the win conditions!");
        // You can trigger a win event here, like UI updates, game end state, etc.
    }



}
