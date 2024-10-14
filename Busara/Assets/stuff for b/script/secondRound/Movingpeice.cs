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
    public slotExtra[] placeSlots;

    public Toggle tog;
    public state state;
    public GameObject TP1;
    public GameObject TP2;

    public Image pan;
    public Image pan1;

    private deckManager deck;
    craftingManager crafting;

    public GameObject pR1;
    public GameObject pR2;

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
        placeSlots = (state == state.playerOne) ? placeSlotsP1 : placeSlotsP2;
        TP1.SetActive(state == state.playerOne);
        TP2.SetActive(state == state.playerTwo);

        // Set placeSlotsP1 to white (active) and placeSlotsP2 to black (inactive) when it's player one's turn
        if (state == state.playerOne)
        {
            foreach (slotExtra slot in placeSlotsP1)
            {
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = Color.white;
                }
            }
            foreach (slotExtra slot in placeSlotsP2)
            {
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }
            pR1.SetActive(true);
            pR2.SetActive(false);
        }

        else if (state == state.playerTwo)
        {
            foreach (slotExtra slot in placeSlotsP2)
            {
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = Color.white;
                }
            }
            foreach (slotExtra slot in placeSlotsP1)
            {
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = new Color(1f, 1f, 1f, 0.5f);
                }
            }
            pR1.SetActive(false);
            pR2.SetActive(true);
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
        state = (state == state.playerOne) ? state.playerTwo : state.playerOne;
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