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
    public bool itemsToTrade = false;


    public string[] forge;

   

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

        if (forge == null)
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
        if (forge == null || Vnum == null || selectedItems.Count < 2)
        {
            Debug.Log("One or more required fields are not assigned or not enough items selected.");
            return;
        }

        forgedItems.Clear();
        List<SelectedItem> itemsList = new List<SelectedItem>();

        foreach (var selectedItem in selectedItems)
        {
            itemsList.Add(selectedItem);
        }
        for (int j = 0; j < itemsList.Count - 1; j++)
        {
            if (itemsList[j].it.Rname == itemsList[j + 1].it.Rname)
            {
                Debug.Log("You can't have the same item forged");
                selectedItems.Clear();
                UpdateSelectedItemsDisplay();
                return;
            }
        }

        // Process items using a for loop
        for (int i = 0; i < itemsList.Count - 1; i++)
        {
            item firstItem = itemsList[i].it;
            item secondItem = itemsList[i + 1].it; // Get the next item to compare

            int firstItemIndex = itemsList[i].index;
            int secondItemIndex = itemsList[i + 1].index;


         
            string currentForge = firstItem.Rname + secondItem.Rname;

            bool forged = false;

            bool inPlayerOneSlot = false;
            bool inPlayerTwoSlot = false;

            if (mpc.placeSlotsP1.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerOneSlot = true; 
            else if (mpc.placeSlotsP2.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerTwoSlot = true;
            if (mpc.placeSlotsP1.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerOneSlot = true;
            else if (mpc.placeSlotsP2.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerTwoSlot = true;


            bool mixedSlots = false;

            // Fire and Water combination (0f, 1w, 2r, 3a)
            if (CheckForgeCombination(forge[0], forge[1], currentForge)) // Fire + Water
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(0, true, true); // Add to both slots if it's mixed
                }
                else
                {
                    UpdateForgeStatus(0, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else if (CheckForgeCombination(forge[0], forge[2], currentForge)) // Fire + Rock
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(1, true, true);
                }
                else
                {
                    UpdateForgeStatus(1, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else if (CheckForgeCombination(forge[0], forge[3], currentForge)) // Fire + Air
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(2, true, true);
                }
                else
                {
                    UpdateForgeStatus(2, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else if (CheckForgeCombination(forge[1], forge[2], currentForge)) // Water + Rock
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(3, true, true);
                }
                else
                {
                    UpdateForgeStatus(3, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else if (CheckForgeCombination(forge[1], forge[3], currentForge)) // Water + Air
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(4, true, true);
                }
                else
                {
                    UpdateForgeStatus(4, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else if (CheckForgeCombination(forge[2], forge[3], currentForge)) // Rock + Air
            {
                forged = true;
                if (mixedSlots)
                {
                    UpdateForgeStatus(5, true, true);
                }
                else
                {
                    UpdateForgeStatus(5, inPlayerOneSlot, inPlayerTwoSlot);
                }
            }
            else
            {
                Debug.Log("Combination does not exist, moving to the next item.");
            }

            if (forged)
            {
                forgedItems.Add(firstItem);
                forgedItems.Add(secondItem);
                RemoveForgedItems();
            }
        }

        mpc.SwitchTurn();
        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
    }


    // Helper method to check forge combination
    private bool CheckForgeCombination(string forgeA, string forgeB, string currentForge)
    {
        return (forgeA + forgeB.ToString() == currentForge || forgeB + forgeA.ToString() == currentForge);
    }

    // Helper method to update the forge status based on player slot
    private void UpdateForgeStatus(int index, bool inPlayerOneSlot, bool inPlayerTwoSlot)
    {
        if (inPlayerOneSlot)
        {
            Vtempo[index]++;
            Vnum[index].text = Vtempo[index].ToString();
        }
        if (inPlayerTwoSlot)
        {
            Vtempo1[index]++;
            Vnum1[index].text = Vtempo1[index].ToString();
        }
    }





    public void Weapon()
    {
        if (selectedItems.Count < 3)
        {
            Debug.Log("not enough to use weapon");
            selectedItems.Clear();
            return;
        }
        else if (selectedItems.Count > 3)
        {
            Debug.Log("only 3 Item must be selected to be used");
            selectedItems.Clear();
            return;
        }

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
                else
                {
                    Debug.Log("Selected Items must be similar to use weapon");
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

    public void Trade()
    {
        if (selectedItems.Count < 1)
        {
            Debug.Log("not enough to use weapon");
            UpdateSelectedItemsDisplay();
            selectedItems.Clear();
            return;
        }
        else if (selectedItems.Count > 1)
        {
            Debug.Log("only 3 Item must be selected to be used");
            UpdateSelectedItemsDisplay();
            selectedItems.Clear();
            return;
        }

        itemsToTrade = false;


        for (int i = 0; i <= selectedItems.Count - 3; i++)
        {
            string currentForge = selectedItems[i].it.Rname + selectedItems[i + 1].it.Rname + selectedItems[i + 2].it.Rname;


            for (int k = 0; k < weapon.Length; k++)
            {
                if (weapon[k] == currentForge && Vnum[k] != null)
                {

                    itemsToTrade = true;

                    forgedItems.Add(selectedItems[i].it);
                    forgedItems.Add(selectedItems[i + 1].it);
                    forgedItems.Add(selectedItems[i + 2].it);

                    break;
                }
                else
                {
                    Debug.Log("Selected Items must be similar to use weapon");
                }
            }

            if (itemsToTrade)
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
                if (selectedItem != null)
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
        // Ensure slot is valid
        if (slot == null) return;

        // Case 1: Selecting an item from a slot
        if (tog.isOn && itemsForged == false && itemsToTrade == false)
        {
                selecte(slot);
        }
        // Case 2: Removing the item from a slot
        else if (tog.isOn && itemsForged == true && itemsToTrade == false)
        {
            WeaponRemove(slot);
        }
        // Cases 3: Picking up an item from a slot or placing it into another empty slot
        else if (!tog.isOn && itemsForged == false && itemsToTrade == false)
        {
            MovePlaced(slot);
        }
        else if(!tog.isOn && itemsForged == false && itemsToTrade == true)
        {
            Exchange(slot);
        }

        UpdateSelectedItemsDisplay();
    }

    public void Exchange(slotExtra slot)
    {
       
    }

    public void WeaponRemove(slotExtra slot)
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

    public void MovePlaced(slotExtra slot)
    {
        if (slot != null && slot.it != null && currentItem == null && IsInPlaceSlot(slot))
        {
            // Case 3: Pick up the item from this slot
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

            // Clear the slot
            slot.it = null;
        }
        else if (currentItem != null && slot.it == null && IsInPlaceSlot(slot))
        {
            // Case 4: Place the item into this slot
            slot.it = currentItem;

            Image slotImage = slot.GetComponent<Image>();
            Image currentItemImage = currentItem.GetComponent<Image>();
            if (slotImage != null && currentItemImage != null)
            {
                slotImage.sprite = currentItemImage.sprite;
            }

            // Clear currentItem after placing it
            currentItem = null;
            currentCourser.gameObject.SetActive(false);
            mpc.SwitchTurn();
        }
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

            // Check adjacency based on the last selected item
            if (selectedItems.Count == 0 || IsAdjacent(slot, selectedItems.Last()))
            {
                selectedItems.Add(new SelectedItem(slot.it, slot.index));

                // Log the index and associated slot
                Debug.Log($"Selected item index: {slot.index}, associated slot: {slot.name}");

                UpdateSelectedItemsDisplay();
            }
            else
            {
                Debug.Log("No, you cannot select that item. It must be adjacent to the last selected item.");
            }
        }
    }

    private bool IsAdjacent(slotExtra slot, SelectedItem lastSelectedItem)
    {
        int currentIndex = System.Array.IndexOf(placeSlotsALL, slot);
        int lastIndex = lastSelectedItem.index;

        if (currentIndex < 0 || lastIndex < 0) return false;

        // Check horizontal adjacency
        if ((currentIndex == lastIndex - 1 && currentIndex % 8 != 7) ||
            (currentIndex == lastIndex + 1 && currentIndex % 8 != 0))
        {
            return true;
        }

        // Check vertical adjacency
        int columnLength = 8;  // The number of columns
        if (currentIndex == lastIndex - columnLength || currentIndex == lastIndex + columnLength)
        {
            return true;
        }

        return false;
    }



    private bool IsInPlaceSlot(slotExtra slot)
    {
        // Check if the slot exists in mpc.placeSlot
        return mpc.placeSlots != null && mpc.placeSlots.Contains(slot);
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
