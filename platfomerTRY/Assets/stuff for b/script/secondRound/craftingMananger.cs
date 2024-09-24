    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;
    using Unity.VisualScripting;

public enum state
{
    start,playerOne, playerTwo, none
}

public class craftingManager : MonoBehaviour
    {
        private item currentItem;
        public Image currentCourser;

        public slotExtra[] placeSlotsP1;
        public slotExtra[] placeSlotsP2;
        public slotExtra[] placeSlots;

        public string[] forges;

        public TMP_Text[] Vnum;
        public float Vcount;
        public float Vtempo0;
        public float Vtempo1;
        public float Vtempo2;
        public float Vtempo3;
        public float Vtempo4;
        public float Vtempo5;
        private Image OgIm;
        public slotExtra Og;
        public Toggle tog;
        public state state;
    public GameObject TP1;
    public GameObject TP2;

        kingdom king;
        deckManager deck;


        private void Start()
        {
        state = state.playerOne;

            if (Og == null || currentCourser == null)
            {
                Debug.LogError("One or more required fields are not assigned in the inspector.");
            }
            else
            {
                OgIm = Og.GetComponent<Image>();
            }

            // Ensure that placeSlotsP1 is not null and contains elements
            if (placeSlotsP1 == null || placeSlotsP1.Length == 0)
            {
                Debug.LogError("placeSlotsP1 is not assigned or empty.");
            }
            if (placeSlotsP2 == null || placeSlotsP2.Length == 0)
            {
                Debug.LogError("placeSlotsP2 is not assigned or empty.");
            }
            // Ensure forges and virtue arrays are properly assigned
            if (forges == null)
            {
                Debug.LogError("forges or virtue arrays are not assigned.");
            }

        deck = FindObjectOfType<deckManager>();
    }

        private void Update()
        {

            if (state == state.playerOne)
            {
                placeSlots = placeSlotsP2;
                TP1.SetActive(true);
                TP2.SetActive(false);
            }
            else if (state == state.playerTwo)
            {
                placeSlots = placeSlotsP1;
                TP2.SetActive(true);
                TP1.SetActive(false);
            }
        movingR();
        }

    public List<item> selectedItems = new List<item>();

    void movingR()
    {


        if (Input.GetMouseButtonUp(0))
        {
            if (currentItem != null)
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
                    nearestSlot.gameObject.SetActive(true);
                    Image slotImage = nearestSlot.GetComponent<Image>();
                    if (slotImage != null && currentItem != null)
                    {
                        slotImage.sprite = currentItem.GetComponent<Image>().sprite;
                        slotImage.color = currentItem.GetComponent<Image>().color;
                
                        
                    }
                    nearestSlot.it = currentItem;
                }

                state = state == state.playerOne ? state.playerTwo : state.playerOne;
                deck.state = state == state.playerOne ? state.playerTwo : state.playerOne;
                currentItem = null;
            }
        }

    }

    public List<item> forgedItems = new List<item>();

    public void CheckForForge()
    {
        if (forges == null || Vnum == null || selectedItems == null || selectedItems.Count < 2)
        {
            Debug.LogError("One or more required fields are not assigned or not enough items selected.");
            return;
        }

        bool itemsForged = false;
        forgedItems.Clear();

        for (int i = 0; i < selectedItems.Count - 1; i++)
        {
            string currentForge = selectedItems[i].Rname + selectedItems[i + 1].Rname;

            for (int k = 0; k < forges.Length; k++)
            {
                if (forges[k] == currentForge && Vnum[k] != null)
                {
                    Vcount = 0;
                    Vcount++;

                    switch (k)
                    {
                        case 0:
                            Vtempo0 += Vcount;
                            Vnum[0].text = Vtempo0.ToString();
                            break;
                        case 1:
                            Vtempo1 += Vcount;
                            Vnum[1].text = Vtempo1.ToString();
                            break;
                        case 2:
                            Vtempo2 += Vcount;
                            Vnum[2].text = Vtempo2.ToString();
                            break;
                        case 3:
                            Vtempo3 += Vcount;
                            Vnum[3].text = Vtempo3.ToString();
                            break;
                        case 4:
                            Vtempo4 += Vcount;
                            Vnum[4].text = Vtempo4.ToString();
                            break;
                        case 5:
                            Vtempo5 += Vcount;
                            Vnum[5].text = Vtempo5.ToString();
                            break;
                    }

                     forgedItems.Add(selectedItems[i]);
                     forgedItems.Add(selectedItems[i + 1]);
                    itemsForged = true;
                }
            }
        }

        if (itemsForged)
        {
            RemoveForgedItems();
            state = state == state.playerOne ? state.playerTwo : state.playerOne;
        }

        selectedItems.Clear();
    }




    void RemoveForgedItems()
    {
        //it should not remove the items that are just similar
        foreach (slotExtra slot in placeSlots)
        {
            if (slot != null && forgedItems.Contains(slot.it))
            {
                
                slot.it = null;

                
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null && OgIm != null)
                {
                    slotImage.sprite = OgIm.sprite;
                    slotImage.color = OgIm.color;
                
                }
            }
        }

       
        foreach (item forgedItem in forgedItems)
        {
            selectedItems.Remove(forgedItem);
        }

        selectedItems.Clear();
        forgedItems.Clear();
    }


    public void OnClickSlot(slotExtra slot)
    {
        if (tog != null && tog.isOn)
        {
            if (slot != null)
            {
                if (slot.it != null)
                {
                    selectedItems.Add(slot.it);
                }
            }
        }
        else
        {
           
            if (slot != null && slot.it != null)
            {
                currentItem = slot.it;

                
                if (currentCourser != null)
                {
                    currentCourser.gameObject.SetActive(true);
                    Image itemImage = currentItem.GetComponent<Image>();
                    if (itemImage != null)
                    {
                        currentCourser.sprite = itemImage.sprite;
                        currentCourser.color = itemImage.color;
                    }
                }

                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null && OgIm != null)
                {
                    slotImage.sprite = OgIm.sprite;
                    slotImage.color = OgIm.color;
    
                }
                slot.it = null;
            }
        }
    }



    public void OnMouseDownItem(item it)
        {
            if (it != null)
            {
                if (currentItem == null)
                {
                    currentItem = it;
                    if (currentCourser != null)
                    {
                        currentCourser.gameObject.SetActive(true);
                        Image itemImage = currentItem.GetComponent<Image>();
                        if (itemImage != null)
                        {
                            currentCourser.sprite = itemImage.sprite;
                            currentCourser.color = itemImage.color;
                        }
                    }
                }
            }
        }
    }
