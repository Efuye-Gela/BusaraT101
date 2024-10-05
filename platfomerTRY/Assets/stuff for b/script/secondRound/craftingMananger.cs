using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class craftingManager : MonoBehaviour
{
    private item currentItem;
    public Image currentCourser;

    public slotExtra[] placeSlotsALL;
    public string[] forges;
    public string[] weapon;
    public TMP_Text[] Vnum;
    public TMP_Text selectedItemsDisplay;

    private float[] Vtempo = new float[12];
    private Image OgIm;
    public slotExtra Og;
    public Toggle tog;
    public state state;

    private deckManager deck;
    public Movingpeice mpc;

    public List<SelectedItem> selectedItems = new List<SelectedItem>();
    public List<item> forgedItems = new List<item>();
    public bool itemsForged = false;


    private void Start()
    {
        state = state.playerOne;

        if (Og == null || currentCourser == null)
        {
            Debug.LogError("One or more required fields are not assigned in the inspector.");
            return;
        }
        OgIm = Og.GetComponent<Image>();

        if (placeSlotsALL.Length == 0)
        {
            Debug.LogError("placeSlotsALL is not assigned or empty.");
            return;
        }

        if (forges == null)
        {
            Debug.LogError("forges array is not assigned.");
            return;
        }

        deck = FindObjectOfType<deckManager>();
        mpc = FindObjectOfType<Movingpeice>();

        UpdateSelectedItemsDisplay();
    }

    private void Update()
    {
        mpc.UpdateGameState();
    }

    public void CheckForForge()
    {
        if (forges == null || Vnum == null || selectedItems.Count < 2)
        {
            Debug.Log("One or more required fields are not assigned or not enough items selected.");
            return;
        }


        forgedItems.Clear();

        Queue<item> itemQueue = new Queue<item>();

        foreach (var selectedItem in selectedItems)
        {
            itemQueue.Enqueue(selectedItem.it);
        }

        // Process each item in the queue
        for (int i = 0; i < itemQueue.Count - 1; i++)
        {
            item firstItem = itemQueue.Dequeue();
            item secondItem = itemQueue.Peek(); // Look at the next item without removing it


            string currentForge = firstItem.Rname + secondItem.Rname;


            bool forged = false;

            for (int k = 0; k < forges.Length; k++)
            {
                if (forges[k] == currentForge && Vnum[k] != null)
                {
                    Vtempo[k]++;
                    Vnum[k].text = Vtempo[k].ToString();

                    forgedItems.Add(firstItem);
                    forgedItems.Add(secondItem);
                    forged = true;
                    break;
                }
            }

            if (forged)
            {
                RemoveForgedItems();
                mpc.SwitchTurn();
            }
            else
            {
                // If not forged, re-add the first item back to the queue
                itemQueue.Enqueue(firstItem);
            }
        }

        // Clear selected items after processing
        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
    }



    public void Weapon()
    {
        itemsForged = false; // Reset the flag before processing

        // Loop through selectedItems ensuring we check groups of three consecutive items
        for (int i = 0; i <= selectedItems.Count - 3; i++)
        {
            // Create a string that combines the Rnames of three consecutive items
            string currentForge = selectedItems[i].it.Rname + selectedItems[i + 1].it.Rname + selectedItems[i + 2].it.Rname;

            // Check if the current combination exists in the 'weapon' array
            for (int k = 0; k < weapon.Length; k++)
            {
                if (weapon[k] == currentForge && Vnum[k] != null)
                {



                    // Set the itemsForged flag to true since we found a valid combination
                    itemsForged = true;

                    // Add the forged items to the forgedItems list
                    forgedItems.Add(selectedItems[i].it);
                    forgedItems.Add(selectedItems[i + 1].it);
                    forgedItems.Add(selectedItems[i + 2].it);

                    break; // Exit the inner loop once the combination is found
                }
            }

            if (itemsForged)
            {
                // If items are forged, remove them and switch turn
                RemoveForgedItems();
                break; // Break the outer loop once forging is successful
            }
        }

        // Clear selected items after processing
        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
    }



    void RemoveForgedItems()
    {
        List<slotExtra> slotsToClear = new List<slotExtra>();

        foreach (slotExtra slot in placeSlotsALL)
        {
            if (slot != null && forgedItems.Contains(slot.it))
            {
                // Check if the slot's index matches the one stored in selectedItems
                SelectedItem selectedItem = selectedItems.Find(si => si.index == slot.index);
                if (IsAdjacent(slot) && selectedItem != null)
                {
                    slotsToClear.Add(slot);
                }
            }
        }

        foreach (slotExtra slot in slotsToClear)
        {
            slot.it = null;

            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null && OgIm != null)
            {
                slotImage.sprite = OgIm.sprite;
            }
        }

        // Remove the forged items from selectedItems
        foreach (item forgedItem in forgedItems)
        {
            selectedItems.RemoveAll(si => si.it == forgedItem);
        }

        forgedItems.Clear();
        UpdateSelectedItemsDisplay();
    }

    public void OnClickSlot(slotExtra slot)
    {
        if (tog != null && tog.isOn && itemsForged == false)
        {
            if (IsAdjacent(slot))
            {
                selecte(slot);
            }
        }
        else if (tog.isOn && itemsForged == true)
        {
            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null && OgIm != null)
            {
                slotImage.sprite = OgIm.sprite;
            }
            slot.it = null;
            itemsForged = false;
            mpc.SwitchTurn();
        }
        else if(!tog.isOn && itemsForged == false)
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
                    }
                }

                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null && OgIm != null)
                {
                    slotImage.sprite = OgIm.sprite;
                }
                slot.it = null;
            }
        }
        UpdateSelectedItemsDisplay();
    }

    public void selecte(slotExtra slot)
    {
        if (slot != null && slot.it != null)
        {
            // Check if the item is already selected
            if (selectedItems.Exists(si => si.index == slot.index))
            {
                Debug.Log("This item is already selected.");
                return;
            }

            if (IsAdjacent(slot))
            {
                selectedItems.Add(new SelectedItem(slot.it, slot.index));
                UpdateSelectedItemsDisplay();
            }
            else
            {
                Debug.Log("No, you cannot select that item. Items must be adjacent.");
            }
        }
    }

    private bool IsAdjacent(slotExtra slot)
    {
        int index = System.Array.IndexOf(placeSlotsALL, slot);

        if (index < 0) return false;

        // Check horizontal adjacency
        if ((index > 0 && placeSlotsALL[index - 1].it != null && (index % 8 != 0)) ||
            (index < placeSlotsALL.Length - 1 && placeSlotsALL[index + 1].it != null && ((index + 1) % 8 != 0)))
        {
            return true;
        }

        // Check vertical adjacency
        int columnLength = 8;
        if (index >= columnLength && placeSlotsALL[index - columnLength].it != null) return true;
        if (index < placeSlotsALL.Length - columnLength && placeSlotsALL[index + columnLength].it != null) return true;

        Debug.Log("No, you cannot select that item. Items must be adjacent.");
        return false;
    }

    private void UpdateSelectedItemsDisplay()
    {
        if (selectedItemsDisplay == null) return;

        selectedItemsDisplay.text = "Selected Items: ";
        foreach (SelectedItem selectedItem in selectedItems)
        {
            selectedItemsDisplay.text += selectedItem.it.Rname + " ";
        }
    }

}

public class SelectedItem
{
    public item it;
    public int index;

    public SelectedItem(item item, int idx)
    {
        it = item;
        index = idx;
    }
}