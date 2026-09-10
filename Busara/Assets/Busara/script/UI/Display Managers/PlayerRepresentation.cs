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
        PowerManager.Instance.InspectPlayer(player);
    }
}
