using TMPro;
using UnityEngine;

public class PlayerRepresentation : MonoBehaviour
{
    public Player player;
    public TMP_Text PlayerName;
    public void playerClicked()
    {
        TurnManager.Instance.ActivePlayer.selectedPlayer = player;
        Debug.Log("Player selected: " + player.Name);
    }
}
