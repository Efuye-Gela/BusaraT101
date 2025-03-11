using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PoliticsDisaster : DisasterEffect
{
    [SerializeField] GameObject BoardsHolder;
    [SerializeField] List<GameObject> boards;
    [SerializeField] float rotateBoardAngle = -90;
    [SerializeField] float roateHolderAngle = 90;
    [SerializeField] bool shiftLeft = false;

    public override void Execute()
    {
        var (Accepted, players) = IsValid(PlayerManager.Instance.Players);
        List<Player> tempPlayers = new List<Player>();
        if (Accepted)
        {           
            PlayerStatDisplay.Instance.Communication("Change your lands !!!");

            // Copy and instantiate players
            foreach (Player player in PlayerManager.Instance.Players)
            {
                Player newPlayer = Instantiate(player);
                tempPlayers.Add(newPlayer);
            }

            if (shiftLeft)
            {
                // Shift Left: Move the first element to the last position
                tempPlayers.Add(tempPlayers[0]);
                tempPlayers.RemoveAt(0);
                BoardsRotateLeft();
            }
            else
            {
                // Shift Right: Move the last element to the first position
                tempPlayers.Insert(0, tempPlayers[tempPlayers.Count - 1]);
                tempPlayers.RemoveAt(tempPlayers.Count - 1);
                BoardsRotateRight();    
            }

            // Reassign boards based on the new order
            foreach (Player player in players)
            {
                player.Board = tempPlayers[player.Board.boardId].Board;
            }
            

            // this is for the board side 
            foreach (Player player in players)
            {
                player.Board.player = player;
            }
            foreach (Player player in tempPlayers)
            {
                Destroy(player.gameObject);
            }

            tempPlayers.Clear();
            
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
           TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }

    }

    private void BoardsRotateLeft()
    {
        if (BoardsHolder.transform.rotation != Quaternion.Euler(0, 0, roateHolderAngle))
        {
            BoardsHolder.transform.DORotate(new Vector3(0, 0, roateHolderAngle), 1f);
            foreach (var board in BoardManager.Instance.gameBoards)
            {
                foreach (var slot in board.Slots)
                {
                    slot.gameObject.transform.rotation = Quaternion.Euler(0, 0, rotateBoardAngle);
                }
            }
        }
    }

    public void BoardsRotateRight()
    {
        if (BoardsHolder.transform.rotation != Quaternion.Euler(0, 0, -roateHolderAngle))
        {
            BoardsHolder.transform.DORotate(new Vector3(0, 0, -roateHolderAngle), 1f);
            foreach (var board in BoardManager.Instance.gameBoards)
            {
                foreach (var slot in board.Slots)
                {
                    slot.gameObject.transform.rotation = Quaternion.Euler(0, 0, -rotateBoardAngle);
                }
            }
        }
    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        if (allPlayers != null)
            return (true, PlayerManager.Instance.Players);
        else
            return (false, PlayerManager.Instance.Players);
    }
    public void BoardMovement()
    {

    }
}
