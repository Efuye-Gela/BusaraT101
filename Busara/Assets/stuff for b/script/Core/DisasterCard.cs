using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DisasterCard : Card
{
    [SerializeField] private DisasterEffect effect;

    public DisasterCard(string cardName, string description, DisasterEffect effect)
        : base(cardName, description)
    {
        this.effect = effect;
    }

    public void ActivatePower(Player currentPlayer, List<Player> allPlayers)
    {
        effect.Execute(currentPlayer, allPlayers);
    }
}