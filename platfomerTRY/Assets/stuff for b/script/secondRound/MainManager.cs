using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum playerstate{playerOne, playerTwo,playerThree, playerFour}
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

    public void Turn()
    {
        //if the code inside of the deckManager and craftManager happens make it change state between the above playerstates where if player on deos any of the code in craftmanager 
    }
}
