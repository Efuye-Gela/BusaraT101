using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class craftingManager : MonoBehaviour
{
    private item currentItem;
    public Image currentCourser;

    public slotExtra[] placeSlotsALL;
    public string[] weapon;
    public string[] tradItem;
    public TMP_Text[] Vnum;
    public TMP_Text[] Vnum1;
    public TMP_Text[] Vnum2;
    public TMP_Text[] Vnum3;

    public TMP_Text selectedItemsDisplay;

    private float[] Vtempo = new float[7];
    private float[] Vtempo1 = new float[7];
    private float[] Vtempo2 = new float[7];
    private float[] Vtempo3 = new float[7];
    private Image OgIm;
    public slotExtra Og;
    public Toggle tog;
    public state state;

    private deckManager deck;
    public Movingpeice mpc;
    MainManager mm;

    public List<SelectedItem> selectedItems = new List<SelectedItem>();
    public List<item> forgedItems = new List<item>();
    public bool itemsForged = false;
    int RemoveCount = 3;
    public bool itemsToTrade = false;


    public string[] forge;
    slotExtra previousSlot = null;
    slotExtra previousSlot1 = null;
    slotExtra previousSlot2 = null;
    slotExtra previousSlot3 = null;


    private slotExtra selectedFirstItemSlot = null;
    public Trading tradingSystem;


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

        tradingSystem = new Trading();
        tradingSystem.placeSlotsALL = placeSlotsALL;
    }

    private void Update()
    {
        mpc.UpdateGameState();
        SomeCraftingMethod();
    }
    public void SomeCraftingMethod()
    {
        foreach (slotExtra slot in mpc.placeSlots)
        {
            // Skip if the slot is null or inactive
            if (slot == null || !slot.gameObject.activeSelf)
            {
                continue; // Skip this iteration
            }

            // Your crafting logic here, operating only on active slots
        }
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
        bool inPlayerOneSlot = false;
        bool inPlayerTwoSlot = false;
        bool inPlayerThreeSlot = false;
        bool inPlayerFourSlot = false;

        // Process items using a for loop
        for (int i = 0; i < itemsList.Count - 1; i++)
        {
            item firstItem = itemsList[i].it;
            item secondItem = itemsList[i + 1].it; // Get the next item to compare

            int firstItemIndex = itemsList[i].index;
            int secondItemIndex = itemsList[i + 1].index;


         
            string currentForge = firstItem.Rname + secondItem.Rname;

            bool forged = false;

         

            //first item check
            if (mpc.placeSlotsP1.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerOneSlot = true; 
            else if (mpc.placeSlotsP2.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerTwoSlot = true;
            else if (mpc.placeSlotsP3.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerThreeSlot = true;
            else if (mpc.placeSlotsP4.Contains(placeSlotsALL[firstItemIndex]))
                inPlayerFourSlot = true;

            //the second item check
            if (mpc.placeSlotsP1.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerOneSlot = true;
            else if (mpc.placeSlotsP2.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerTwoSlot = true;
            if (mpc.placeSlotsP3.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerThreeSlot = true;
            else if (mpc.placeSlotsP4.Contains(placeSlotsALL[secondItemIndex]))
                inPlayerFourSlot = true;




            // Fire and Water combination (0f, 1w, 2r, 3a)
            if (CheckForgeCombination(forge[0], forge[1], currentForge)) // Fire + Water
            {
                forged = true;  
                UpdateForgeStatus(0, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
                
            }
            else if (CheckForgeCombination(forge[0], forge[2], currentForge)) // Fire + Rock
            {
                forged = true;
               UpdateForgeStatus(1, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
            }
            else if (CheckForgeCombination(forge[0], forge[3], currentForge)) // Fire + Air
            {
                forged = true;
                UpdateForgeStatus(2, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
            }
            else if (CheckForgeCombination(forge[1], forge[2], currentForge)) // Water + Rock
            {
                forged = true;
                UpdateForgeStatus(3, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
            }
            else if (CheckForgeCombination(forge[1], forge[3], currentForge)) // Water + Air
            {
                forged = true;
                UpdateForgeStatus(4, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
            }
            else if (CheckForgeCombination(forge[2], forge[3], currentForge)) // Rock + Air
            {
                forged = true;
                UpdateForgeStatus(5, inPlayerOneSlot, inPlayerTwoSlot, inPlayerThreeSlot, inPlayerFourSlot);
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
    private void UpdateForgeStatus(int index, bool inPlayerOneSlot, bool inPlayerTwoSlot, bool inPlayerThreeSlot, bool inPlayerFourSlot)
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
        if (inPlayerThreeSlot)
        {
            Vtempo2[index]++;
            Vnum2[index].text = Vtempo2[index].ToString();
        }
        if (inPlayerFourSlot)
        {
            Vtempo3[index]++;
            Vnum3[index].text = Vtempo3[index].ToString();
        }
    }





    public void Weapon()
    {
        if (selectedItems.Count != 3)
        {
            Debug.Log("Exactly 3 items must be selected to use weapon");
            selectedItems.Clear();
            return;
        }
        if(previousSlot != null || previousSlot1 != null)
        {
            previousSlot = null;
            previousSlot1 = null;
        }
        if (previousSlot2 != null || previousSlot3 != null)
        {
            previousSlot2 = null;
            previousSlot3 = null;
        }

        // Check if selected items belong to another player's resources
        foreach (var item in selectedItems)
        {
            // Assuming `mpc.placeSlots` is a list of slots with an index and player ownership
            if (!mpc.placeSlots.Any(slot => slot.index == item.index))
            {
                Debug.Log("You cannot use another player's resource for weapon");
                selectedItems.Clear();
                UpdateSelectedItemsDisplay();
                return;
            }
        }

         itemsForged = false;

        // Create a string representing the current selected items
        string currentForge = selectedItems[0].it.Rname + selectedItems[1].it.Rname + selectedItems[2].it.Rname;

        // Iterate through available weapons to find a match
        for (int k = 0; k < weapon.Length; k++)
        {
            if (weapon[k] == currentForge && Vnum[k] != null)
            {
                itemsForged = true;
                RemoveCount = 3;

                // Add the forged items
                forgedItems.Add(selectedItems[0].it);
                forgedItems.Add(selectedItems[1].it);
                forgedItems.Add(selectedItems[2].it);

                break; // Weapon has been forged, break the loop
            }
        }

        if (itemsForged)
        {
            Debug.Log("Weapon forged successfully!");
            RemoveForgedItems(); // Remove forged items
        }
        else
        {
            Debug.Log("Selected items must be similar to forge a weapon");
        }

        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
    }


    public void TradeSelectedItems(slotExtra slot)
    {
        if (itemsToTrade && selectedFirstItemSlot == null && slot.it != null)
        {
            // First item selection for trading
            selectedFirstItemSlot = slot;
            selectedItems.Add(new SelectedItem(slot.it, slot.index));
            Debug.Log($"First trade item selected: {slot.it.Rname}");
            UpdateSelectedItemsDisplay();
        }
        else if (itemsToTrade && selectedFirstItemSlot != null && slot.it != null)
        {
            // Second item selection for trading
            tradingSystem.TradeItems(selectedFirstItemSlot.index, slot.index);
            selectedFirstItemSlot = null; 
            itemsToTrade = false; 
            selectedItems.Clear();
            UpdateSelectedItemsDisplay();
            mpc.SwitchTurn();
        }
        else
        {
            Debug.Log("Invalid action. Please select a valid item or press trade.");
        }
    }

    
    public void OnTradeButtonPressed()
    {
        selectedItems.Clear();
        UpdateSelectedItemsDisplay();
        itemsToTrade = true;
        Debug.Log("Selecte your item for trade:");
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
        else if(tog.isOn && itemsForged == false && itemsToTrade == true)
        {
            TradeSelectedItems(slot);
        }

        UpdateSelectedItemsDisplay();
    }






    public void WeaponRemove(slotExtra slot)
    {
        // Ensure slot and item are valid
        if (slot == null || slot.it == null) return;



        slotExtra originalSlot = placeSlotsALL.FirstOrDefault(s => s.index == slot.index);
        
        if (originalSlot != null)
        {
            Debug.Log($"Item removed from slot {originalSlot.name} (index {slot.index}).");
        }

        if (mpc.placeSlotsP1.Contains(previousSlot) && mpc.placeSlotsP1.Contains(originalSlot))
        {
            Debug.Log("You have already removed from here.");
            return;
        }
        else if (mpc.placeSlotsP2.Contains(previousSlot1) && mpc.placeSlotsP2.Contains(originalSlot))
        {
            Debug.Log("You have already removed from here.");
            return;
        }
        if (mpc.placeSlotsP3.Contains(previousSlot2) && mpc.placeSlotsP3.Contains(originalSlot))
        {
            Debug.Log("You have already removed from here.");
            return;
        }
        if (mpc.placeSlotsP4.Contains(previousSlot3) && mpc.placeSlotsP4.Contains(originalSlot))
        {
            Debug.Log("You have already removed from here.");
            return;
        }
        if(mpc.placeSlotsP1.Contains(originalSlot))
            previousSlot = originalSlot;
        if (mpc.placeSlotsP2.Contains(originalSlot))
            previousSlot1 = originalSlot;
        if (mpc.placeSlotsP3.Contains(originalSlot))
            previousSlot2 = originalSlot; 
        if (mpc.placeSlotsP4.Contains(originalSlot))
            previousSlot3 = originalSlot;

        // Reset slot image if possible
        Image slotImage = slot.GetComponent<Image>();
        if (slotImage != null && OgIm != null)
        {
            slotImage.sprite = OgIm.sprite;
            RemoveCount--;
        }

        // Clear the item from the slot after all operations
        slot.it = null;

        // Switch turn when RemoveCount reaches 0
        if (RemoveCount == 0)
        {
            itemsForged = false;
            RemoveCount = 0;
            mpc.SwitchTurn();
        }
    }





    public void MovePlaced(slotExtra slot)
    {
        if (slot != null && slot.it != null && currentItem == null && IsInPlaceSlot(slot))
        {
            // Case 3: Pick up the item from this slot
            currentItem = slot.it;
            selectedItems.Add(new SelectedItem(slot.it, slot.index));

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
        else if (currentItem != null && slot.it == null && IsInPlaceSlot(slot) && IsAdjacent(slot, selectedItems.Last()))
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
            if (selectedItems.Count == 0)
            {
                if (!IsInPlaceSlot(slot))
                {
                    Debug.Log("The first selected item must be within mpc.placeSlot.");
                    return;
                }
            }

            // Check if the item is already selected
            var selected = selectedItems.FirstOrDefault(si => si.index == slot.index);
            if (selected != null)
            {
                
                selectedItems.Remove(selected);
                Debug.Log("Item Removed!");

                UpdateSelectedItemsDisplay();
            }
            else if (selectedItems.Count == 0 || IsAdjacent(slot, selectedItems.Last()))
            {
                
                selectedItems.Add(new SelectedItem(slot.it, slot.index));

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

    public void UpdateSelectedItemsDisplay()
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
public class Trading
{
    public slotExtra[] placeSlotsALL;

   
    public void TradeItems(int index1, int index2)
    {
       
        if (index1 < 0 || index1 >= placeSlotsALL.Length || index2 < 0 || index2 >= placeSlotsALL.Length)
        {
            Debug.LogError("Invalid indices for item trade.");
            return;
        }

        slotExtra slot1 = placeSlotsALL[index1];
        slotExtra slot2 = placeSlotsALL[index2];

        
        if (slot1.it == null || slot2.it == null)
        {
            Debug.LogError("Cannot trade. One or both slots are empty.");
            return;
        }

       
        item tempItem = slot1.it;
        slot1.it = slot2.it;
        slot2.it = tempItem;

        // Optional: Update the slot visuals (e.g., image sprites)
        UpdateSlotVisuals(slot1);
        UpdateSlotVisuals(slot2);

        Debug.Log($"Traded items between slots {index1} and {index2}.");
    }

    // Helper function to update slot visuals after a trade
    private void UpdateSlotVisuals(slotExtra slot)
    {
        if (slot == null) return;

        Image slotImage = slot.GetComponent<Image>();
        if (slotImage != null && slot.it != null)
        {
            Image itemImage = slot.it.GetComponent<Image>();
            if (itemImage != null)
            {
                slotImage.sprite = itemImage.sprite;
            }
        }
    }
}


