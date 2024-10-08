using System.Collections.Generic;
using System.Linq;
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
    public TMP_Text[] Vnum1;
    public TMP_Text selectedItemsDisplay;

    private float[] Vtempo = new float[7];
    private float[] Vtempo1 = new float[7];
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

        // Process items in the queue
        while (itemQueue.Count > 1)
        {
            item firstItem = itemQueue.Dequeue();
            item secondItem = itemQueue.Peek();  // Get the next item to compare

            string currentForge = firstItem.Rname + secondItem.Rname;

            bool forged = false;

            for (int k = 0; k < forges.Length; k++)
            {
                // Wrap the index if it exceeds the array length (0 to 4)
                int wrappedIndex = k % Vtempo.Length;  // or Vtempo1.Length, they seem to have the same size

                if (forges[k] == currentForge && Vnum[wrappedIndex] != null && Vnum1[wrappedIndex] != null)
                {
                    if (mpc.state == state.playerOne)
                    {
                        Vtempo[wrappedIndex]++;
                        Vnum[wrappedIndex].text = Vtempo[wrappedIndex].ToString();
                    }
                    else if (mpc.state == state.playerTwo)
                    {
                        Vtempo1[wrappedIndex]++;
                        Vnum1[wrappedIndex].text = Vtempo1[wrappedIndex].ToString();
                    }

                    forgedItems.Add(firstItem);
                    forgedItems.Add(secondItem);
                    forged = true;
                    break;
                }
            }

            if (forged)
            {
                RemoveForgedItems();
            }
            else
            {
                // If no forge, re-enqueue the first item at the end
                itemQueue.Enqueue(firstItem);
            }
        }

        // Clear selected items after processing
        mpc.SwitchTurn();
        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
    }



    public void Weapon()
    {
        itemsForged = false;

        
        for (int i = 0; i <= selectedItems.Count - 3; i++)
        {
            string currentForge = selectedItems[i].it.Rname + selectedItems[i + 1].it.Rname + selectedItems[i + 2].it.Rname;

          
            for (int k = 0; k < weapon.Length; k++)
            {
                if (weapon[k] == currentForge && Vnum[k] != null)
                {
                   
                    itemsForged = true;


                    forgedItems.Add(selectedItems[i].it);
                    forgedItems.Add(selectedItems[i + 1].it);
                    forgedItems.Add(selectedItems[i + 2].it);

                    break; 
                }
            }

            if (itemsForged)
            {
              
                RemoveForgedItems();
                break;
            }
        }
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
        else if (!tog.isOn && itemsForged == false)
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

            // Check if it's the first item being selected
            if (selectedItems.Count == 0)
            {
                // Ensure the first selected item is within mpc.placeSlot
                if (!IsInPlaceSlot(slot))
                {
                    Debug.Log("The first selected item must be within mpc.placeSlot.");
                    return;
                }
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

    private bool IsInPlaceSlot(slotExtra slot)
    {
        // Check if the slot exists in mpc.placeSlot
        return mpc.placeSlots != null && mpc.placeSlots.Contains(slot);
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
