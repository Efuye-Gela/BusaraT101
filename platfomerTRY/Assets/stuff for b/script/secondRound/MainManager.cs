using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public enum state
{
    start, playerOne, playerTwo, none
}

public class MainManager : MonoBehaviour
{
    public float N_actions = 1;
    craftingManager craftingManager;
    deckManager deckManager;

 
    private void Start()
    {
        deckManager = GetComponent<deckManager>();
        craftingManager = GetComponent<craftingManager>();
    }
}
