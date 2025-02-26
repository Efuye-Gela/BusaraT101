using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Card : MonoBehaviour
{
    public string CardName;
    [TextArea]
    public string Description;
    protected Card(string cardName, string description)
    {
        CardName = cardName;
        Description = description;
    }

    public static string ShowCard(Card card)
    { 
        return card.CardName;
    }
}