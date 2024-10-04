using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class craftingManager : MonoBehaviour
{
    private item currentItem;
    public Image currentCourser;

    public slotExtra[] placeSlotsALL;

    public string[] forges;
    public string[] weapons;
    public TMP_Text[] Vnum;
    public TMP_Text selectedItemsDisplay; // Add this for the selected items display

    private float[] Vtempo = new float[12];
    private Image OgIm;
    public slotExtra Og;
    public Toggle tog;
    public state state;

    private deckManager deck;
    public Movingpeice mpc;

    public List<SelectedItem> selectedItems = new List<SelectedItem>();
    public List<item> forgedItems = new List<item>();

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

        // Initialize selected items display if assigned
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

        bool itemsForged = false;
        forgedItems.Clear();

        for (int i = 0; i < selectedItems.Count - 1; i++)
        {
            string currentForge = selectedItems[i].it.Rname + selectedItems[i + 1].it.Rname;

            for (int k = 0; k < forges.Length; k++)
            {
                if (forges[k] == currentForge && Vnum[k] != null)
                {
                    Vtempo[k]++;
                    Vnum[k].text = Vtempo[k].ToString();

                    forgedItems.Add(selectedItems[i].it);
                    forgedItems.Add(selectedItems[i + 1].it);
                    itemsForged = true;
                }
            }
        }

        if (itemsForged)
        {
            RemoveForgedItems();
            mpc.SwitchTurn();
        }

        selectedItems.Clear();
        UpdateSelectedItemsDisplay(); // Update display when items are cleared
    }

    void RemoveForgedItems()
    {
        List<slotExtra> slotsToClear = new List<slotExtra>();

        foreach (slotExtra slot in placeSlotsALL)
        {
            if (slot != null && forgedItems.Contains(slot.it))
            {
                // Check if the slot's index matches the one stored in selectedItems
                SelectedItem selectedItem = selectedItems.Find(si => si.it == slot.it && si.index == slot.index);
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

        // Remove the forged items from selectedItems by matching both the item and its index
        foreach (item forgedItem in forgedItems)
        {
            selectedItems.RemoveAll(si => si.it == forgedItem);
        }

        forgedItems.Clear();
        UpdateSelectedItemsDisplay(); // Update display when items are removed
    }

    public void OnClickSlot(slotExtra slot)
    {
        if (tog != null && tog.isOn)
        {
            selecte(slot);
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
        UpdateSelectedItemsDisplay(); // Update display when an item is clicked
    }

    public void selecte(slotExtra slot)
    {
        if (slot != null && slot.it != null)
        {
            if (IsAdjacent(slot))
            {
                // Store both the item and its slot index
                selectedItems.Add(new SelectedItem(slot.it, slot.index));
            }
            else
            {
                Debug.Log("No, you cannot select that item. Items must be adjacent.");
            }
        }
        UpdateSelectedItemsDisplay(); // Update display when an item is selected
    }

    private bool IsAdjacent(slotExtra slot)
    {
        int index = System.Array.IndexOf(placeSlotsALL, slot);

        if (index < 0) return false;

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
