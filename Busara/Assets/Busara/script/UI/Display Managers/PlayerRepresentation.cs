using TMPro;
using UnityEngine;

public class PlayerRepresentation : MonoBehaviour
{
    public Player player;
    public TMP_Text PlayerName;

    public void Start()
    {
        PlayerName.text = player.Name;
    }
    public void playerClicked()
    {
        TurnManager.Instance.ActivePlayer.selectedPlayer = player;
        Debug.Log("Player selected: " + player.Name);
        PowerUIManager.Instance.PowerMassage($"you have selected: {player.Name} to use your power on");
    }
}
