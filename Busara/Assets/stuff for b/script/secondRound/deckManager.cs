using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.VisualScripting;

public class deckManager : MonoBehaviour
{
    public Cards[] cards;
    public item[] items;
    public GameObject lidF;
    public GameObject lidW;
    public GameObject lidA;
    public GameObject lidR;
    public GameObject Button;
    

    public bool lidbool =  true;
    public state state;

    int count = 24;
    public TMP_Text resorcCeount; 

    private void Start()
    {
        Shuffle();
    }
   
    private void Update()
    {
        if(state == state.playerOne)
        {
            Button.SetActive(true);
            lidR.SetActive(false);
            lidA.SetActive(false);
            lidW.SetActive(false);
            lidF.SetActive(false);

        }
        else if(state == state.playerTwo)
        {
            Button.SetActive(true);
            lidR.SetActive(false);
            lidA.SetActive(false);
            lidW.SetActive(false);
            lidF.SetActive(false);
        }
        else if (state == state.playerThree)
        {
            Button.SetActive(true);
            lidR.SetActive(false);
            lidA.SetActive(false);
            lidW.SetActive(false);
            lidF.SetActive(false);
        }
        else if (state == state.playerFour)
        {
            Button.SetActive(true);
            lidR.SetActive(false);
            lidA.SetActive(false);
            lidW.SetActive(false);
            lidF.SetActive(false);
        }
        if (count == 0)
        {
            count = 24;
            Shuffle();
        }

    }
    void Shuffle()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            int randomIndex = Random.Range(0, cards.Length);
            Cards temp = cards[i];
            cards[i] = cards[randomIndex];
            cards[randomIndex] = temp;
        }
    }


    public void Deal()
    {
        if (cards.Length == 0) return;
        Cards topCard = cards[0];
            

        foreach (item item in items)
        {
            if (topCard.RCname == item.Rname)
            {

                Debug.Log("You can use: " + item.Rname);
                if (item.Rname == "rock")
                {
                    lidR.SetActive(lidbool);
                }
                else if (item.Rname == "fire")
                {
                    lidF.SetActive(lidbool);
                }
                else if (item.Rname == "water")
                {
                    lidW.SetActive(lidbool);
                }
                else if (item.Rname == "air")
                {
                    lidA.SetActive(lidbool);
                }
            }
        }



        List<Cards> cardList = new List<Cards>(cards);
        cardList.RemoveAt(0); 
        cardList.Add(topCard); 
        cards = cardList.ToArray();

        Button.SetActive(false);
        state = state.none;
        count--;
        resorcCeount.text = count.ToString() + "/24";
     

    }

 
}
