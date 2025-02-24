using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Power/Imagination")]
public class Imagination : Power
{
    public Imagination(string powerName, string powerDescription) : base(powerName, powerDescription)
    {

    }
    public override bool IsVaild(List<Virtue> virtue)
    {
        if (TurnManager.Instance.ActivePlayer)
        {
            if (virtue.Count > 0)
            {
                virtue.Clear();
                return (true);
            }
            else
                return (false);
        }
        else
            return (false);
    }
    public override void Excute()
    {
        if (IsVaild(TurnManager.Instance.ActivePlayer.selectedVirtues))
        {
            if (TurnManager.Instance.ActivePlayer.selectedPlayers.Count > 0)
                Debug.Log("Please selecte a player u wish to use ur power on");
            else
            {
                if (TurnManager.Instance.ActivePlayer.selectedPlayers[0].Virtues.Count > 0)
                {
                    Debug.Log("Give me your virtue bitch");
                    TurnManager.Instance.ActivePlayer.Virtues.Add(TurnManager.Instance.ActivePlayer.selectedPlayers[0].Virtues[0]);
                    TurnManager.Instance.ActivePlayer.selectedPlayers[0].Virtues.Remove(TurnManager.Instance.ActivePlayer.selectedPlayers[0].Virtues[0]);
                    TurnManager.Instance.CompleteTurn(TurnManager.Instance.ActivePlayer);
                }
            }
        }
        else
        {
            Debug.Log("YOU CAN NOT USE UR POWER JUST YET");
        }
    }
}
