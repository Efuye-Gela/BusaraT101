using UnityEngine;
using System.Collections.Generic;

public class DeckManager : Manager<DeckManager>
{
    public List<Card> Cards;
    public int CardCount = 0;

    private void Start()
    {
        Shuffle();
        CardCount = Cards.Count;
    }

    void Shuffle()
    {
        System.Random random = new System.Random();

        for (int i = 0; i < Cards.Count; i++)
        {
            int j = random.Next(i, Cards.Count);
            Card temporary = Cards[i];
            Cards[i] = Cards[j];
            Cards[j] = temporary;
        }
        ShowCards(Cards);
    }

    public Card Draw()
    {

        if (Cards[0] != null)
        { 
            return Cards[0];
        }
        else
        {
            Debug.LogError("Card Not Found");
            return default(Card);
        } 
    }

    
    public static void ShowCards(List<Card> cards)
    {
        string info = "";
        for (int i = 0; i < cards.Count; i++)
        {
            if (i < cards.Count - 1)
            {
                info += Card.ShowCard(cards[i]) + " ,";
            }
            else
                info += Card.ShowCard(cards[i]);
        }

        Debug.Log(info);
    }

    public void GoToNext()
    {
        Card firstItem = Cards[0]; 
        Cards.RemoveAt(0);
        Cards.Add(firstItem);
        if (CardCount == 0)
        {
            Debug.LogWarning("Deck is empty, reshuffling...");
            Shuffle();
            CardCount = Cards.Count;
        }
        else
        {
            CardCount--;
        }
    }
}
