using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Mathematics;

public class Movingpeice : MonoBehaviour
{
    private item currentItem;
    public Image currentCourser;

    public slotExtra[] placeSlotsP1;
    public slotExtra[] placeSlotsP2;
    public slotExtra[] placeSlotsP3;
    public slotExtra[] placeSlotsP4;
    public slotExtra[] placeSlots;

    public Toggle tog;
    public state state;
    public GameObject TP1;
    public GameObject TP2;
    public GameObject TP3;
    public GameObject TP4;

    private deckManager deck;
    craftingManager crafting;

    public GameObject pR1;
    public GameObject pR2;
    public GameObject pR3;
    public GameObject pR4;

    private void Start()
    {
        state = state.playerOne;

        if (currentCourser == null)
        {
            Debug.LogError("One or more required fields are not assigned in the inspector.");
            return;
        }

        if (placeSlotsP1.Length == 0 || placeSlotsP2.Length == 0)
        {
            Debug.LogError("placeSlotsP1 or placeSlotsP2 is not assigned or empty.");
            return;
        }

        deck = FindObjectOfType<deckManager>();
        crafting = FindObjectOfType<craftingManager>();
    }

    private void Update()
    {
        UpdateGameState();
        movingR();
    }

    public void UpdateGameState()
    {
        // Assign the appropriate placeSlots based on the current player state
        if (state == state.playerOne)
        {
            placeSlots = placeSlotsP1;
        }
        else if (state == state.playerTwo)
        {
            placeSlots = placeSlotsP2;
        }
        else if (state == state.playerThree)
        {
            placeSlots = placeSlotsP3;
        }
        else if (state == state.playerFour)
        {
            placeSlots = placeSlotsP4;
        }

        
        TP1.SetActive(state == state.playerOne);
        TP2.SetActive(state == state.playerTwo);
        TP3.SetActive(state == state.playerThree);
        TP4.SetActive(state == state.playerFour);

        SetSlotColors(placeSlotsP1, state == state.playerOne ? Color.white : new Color(1f, 1f, 1f, 0.2f));
        SetSlotColors(placeSlotsP2, state == state.playerTwo ? Color.white : new Color(1f, 1f, 1f, 0.2f));
        SetSlotColors(placeSlotsP3, state == state.playerThree ? Color.white : new Color(1f, 1f, 1f, 0.2f));
        SetSlotColors(placeSlotsP4, state == state.playerFour ? Color.white : new Color(1f, 1f, 1f, 0.2f));

        pR1.SetActive(state == state.playerOne);
        pR2.SetActive(state == state.playerTwo);
        pR3.SetActive(state == state.playerThree);
        pR4.SetActive(state == state.playerFour);
    }

    // Helper function to set the color of the slots
    private void SetSlotColors(slotExtra[] slots, Color color)
    {
        foreach (slotExtra slot in slots)
        {
            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null)
            {
                slotImage.color = color;
            }
        }
    }



    private void movingR()
    {
        if (Input.GetMouseButtonUp(0) && currentItem != null)
        {
            currentCourser.gameObject.SetActive(false);
            slotExtra nearestSlot = null;
            float shortestDistance = float.MaxValue;

            foreach (slotExtra slotM in placeSlots)
            {
                if (slotM != null)
                {
                    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, slotM.transform.position);
                    float distance = Vector2.Distance(Input.mousePosition, screenPoint);

                    if (distance < shortestDistance)
                    {
                        shortestDistance = distance;
                        nearestSlot = slotM;
                    }
                }
            }

            if (nearestSlot != null)
            {
                // Check if the nearest slot is already occupied
                if (nearestSlot.it != null)
                {
                    Debug.Log("This slot is already occupied.");
                    currentCourser.gameObject.SetActive(true);
                }
                else
                {
                    nearestSlot.gameObject.SetActive(true);
                    Image slotImage = nearestSlot.GetComponent<Image>();
                    if (slotImage != null)
                    {
                        slotImage.sprite = currentItem.GetComponent<Image>().sprite;
                    }
                    nearestSlot.it = currentItem;
                    SwitchTurn();
                    currentItem = null;
                   
                }
            }
        }
    }


    public void SwitchTurn()
    {
        
        if(state == state.playerOne)
        {
            state = state.playerTwo;
        }
        else if(state == state.playerTwo)
        {
            state = state.playerThree;
        }
        else if( state == state.playerThree)
        {
            state = state.playerFour;
        }
        else if(state == state.playerFour)
        {
            state = state.playerOne;
        }

        deck.state = state;
        UpdateUIForCurrentState();
        crafting.selectedItems.Clear();

    }

    private void UpdateUIForCurrentState()
    {
        // Logic to update the UI based on the current player's turn
    }

    public void OnMouseDownItem(item it)
    {
        if (it != null && currentItem == null)
        {
            currentItem = it;
            if (currentCourser != null)
            {
                currentCourser.gameObject.SetActive(true);
                Image itemImage = currentItem.GetComponent<Image>();
                if (itemImage != null)
                {
                    currentCourser.sprite = itemImage.sprite;
                }
            }
        }
    }

}