using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DisasterCard : Card
{
    [SerializeField] public DisasterEffect effect;

    public DisasterCard(string cardName, string description, DisasterEffect effect)
        : base(cardName, description)
    {
        this.effect = effect;
    }
}   