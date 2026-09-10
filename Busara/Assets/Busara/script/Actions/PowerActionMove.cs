using System.Collections.Generic;
using UnityEngine;

public class PowerActionMove : MonoBehaviour
{
    public void OnTapPower()
    {
        Player player = TurnManager.Instance.ActivePlayer;
        PowerManager.Instance.ActivatePower(player != null && player.Kingdom != null ? player.Kingdom.power : null);
    }

}
