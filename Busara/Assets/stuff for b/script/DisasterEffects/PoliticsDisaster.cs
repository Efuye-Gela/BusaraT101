using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PoliticsDisaster : DisasterEffect
{
    public RectTransform BoardHolder;
    public List<GameObject> BoardCollection;
    public List<GameObject> tempBoardCollection;
    public Transform BoardPosition;
    private void Start()
    {
    }
    public override void Execute()
    {
        var (Accepted, players) = IsValid(PlayerManager.Instance.Players);
        List<Player> tempPlayers = new List<Player>();
        if (Accepted)
        {

            PlayerStatDisplay.Instance.Communication("Change your land bitch!!!");
            foreach (Player player in PlayerManager.Instance.Players)
            {
                Player newPlayer = Instantiate(player);
                tempPlayers.Add(newPlayer);
            }
            tempPlayers.Add(tempPlayers[0]);
            tempPlayers.Remove(tempPlayers[0]);

            //this is for the player side 
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

            tempBoardCollection = new List<GameObject>(BoardCollection);
            tempBoardCollection.Add(tempBoardCollection[0]);
            tempBoardCollection.Remove(tempBoardCollection[0]);
            foreach(GameObject board in BoardCollection)
            {
                Destroy(board.gameObject);
            }

            foreach(GameObject board in tempBoardCollection)
            {
               Instantiate(board, BoardPosition);
            }

            tempPlayers.Clear();
            TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }
        else
        {
           TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
        }

    }

    public override (bool, List<Player>) IsValid(List<Player> allPlayers)
    {
        if (allPlayers != null)
            return (true, PlayerManager.Instance.Players);
        else
            return (false, PlayerManager.Instance.Players);
    }
}
