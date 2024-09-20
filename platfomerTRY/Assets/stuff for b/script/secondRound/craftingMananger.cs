    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;

    public class craftingManager : MonoBehaviour
    {
        private item currentItem;
        public Image currentCourser;

        public slotExtra[] placeSlots;

        public string[] forges;

        public TMP_Text[] Vnum;
        public float Vcount;
        public float Vtempo0;
        public float Vtempo1;
        public float Vtempo2;
        private Image OgIm;
        public slotExtra Og;
        public Toggle tog;

        private void Start()
        {

            if (Og == null || currentCourser == null)
            {
                Debug.LogError("One or more required fields are not assigned in the inspector.");
            }
            else
            {
                OgIm = Og.GetComponent<Image>();
            }

            // Ensure that placeSlots is not null and contains elements
            if (placeSlots == null || placeSlots.Length == 0)
            {
                Debug.LogError("placeSlots is not assigned or empty.");
            }

            // Ensure forges and virtue arrays are properly assigned
            if (forges == null)
            {
                Debug.LogError("forges or virtue arrays are not assigned.");
            }
        }

        private void Update()
        {
        movingR();
    ;   }

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
            // Only check the current item and the next item
            string currentForge = selectedItems[i].Rname + selectedItems[i + 1].Rname;

            // Check if this combination matches any in the forges array
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
                    }

                    // Add the forged items to the list
                    forgedItems.Add(selectedItems[i]);
                    forgedItems.Add(selectedItems[i + 1]);
                    itemsForged = true;
                }
            }
        }

        if (itemsForged)
        {
            RemoveForgedItems();
        }
        selectedItems.Clear();
    }




    void RemoveForgedItems()
    {
        foreach (slotExtra slot in placeSlots)
        {
            if (slot != null && forgedItems.Contains(slot.it))
            {
                // Remove the forged item from the slot
                slot.it = null;

                // Restore the original image and color (use OgIm for the default appearance)
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null && OgIm != null)
                {
                    slotImage.sprite = OgIm.sprite;
                    slotImage.color = OgIm.color;
                }
            }
        }

        // Remove forged items from selectedItems
        foreach (item forgedItem in forgedItems)
        {
            selectedItems.Remove(forgedItem);
        }

        // Clear the forgedItems list
        selectedItems.Clear();
        forgedItems.Clear();
    }


    public void OnClickSlot(slotExtra slot)
    {
        if (tog != null && tog.isOn)
        {
            if (slot != null)
            {
                selectedItems.Add(slot.it);
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
