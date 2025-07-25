using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Board : MonoBehaviour, TurnManager.TurnBeginListener, TurnManager.TurnEndListener
{
    public Player player;
    public int boardId;
    public List<Slot> Slots;
    public Image Highlight;
    public TMP_Text BoardOwnerName;
    private Coroutine highlightCoroutine;
    private void Start()
    {
        BoardOwnerName.text = player.Name;
        TurnManager.Instance.AddTurnEndListeners(this);
        TurnManager.Instance.AddTurnBeginListeners(this);
    }
    public Slot GetSlotByIndex(int searchIndex)
    {
        return Slots.FirstOrDefault(sl => sl.Index == searchIndex);
    }

    public List<Slot> GetOccupiedSlots()
    {
        List<Slot> occupiedSlots = new List<Slot>();
        foreach (var slot in Slots)
        {
            if (slot.isOccupied)
            {
                occupiedSlots.Add(slot);
            }
        }
        return occupiedSlots;
    }

    public List<Slot> GetOccupiedSlotByResourceType(ResourceType type)
    {
        List<Slot> slotsOfType = new List<Slot>();
        List<Slot> occupiedSlots = new List<Slot>();

        occupiedSlots = GetOccupiedSlots();
        foreach (var slot in occupiedSlots)
        {
            if (slot.resource != null && slot.resource.resourceType == type)
                slotsOfType.Add(slot);
        }
        return slotsOfType;
    }

    public static void MoveResource(Resource tobeMovedResource, Slot targetSlot)
    {
        Slot.EmptySlotByResource(tobeMovedResource);
        Slot.OccupySlot(targetSlot, tobeMovedResource);
        tobeMovedResource.gameObject.transform.SetParent(targetSlot.gameObject.transform, true);
        tobeMovedResource.gameObject.transform.localPosition = Vector3.zero;
        tobeMovedResource.UnHighlight();
    }

    public static void PlaceResource(Resource tobePlacedResource, Slot destinationSlot)
    {
        tobePlacedResource.gameObject.transform.SetParent(destinationSlot.gameObject.transform, true);
        tobePlacedResource.gameObject.transform.localPosition = Vector3.zero;
        tobePlacedResource.gameObject.transform.localScale = Vector3.one;
        destinationSlot.isOccupied = true;
        destinationSlot.resource = tobePlacedResource;
        tobePlacedResource.slot = destinationSlot;
        tobePlacedResource = null;
        destinationSlot = null;
    }

    public void HighlightBoard()
    {
        // Check if the coroutine is not already running to avoid multiple instances
        if (highlightCoroutine == null)
        {
            highlightCoroutine = StartCoroutine(FadeHighlight());
        }
    }
    public void UnHighlightBoard()
    {
        // If the coroutine is running, stop it
        if (highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            highlightCoroutine = null;
        }
        // Ensure the highlight is turned off
        Highlight.gameObject.SetActive(false);
    }

    private IEnumerator FadeHighlight()
    {
        float fadeDuration = 0.7f;
        Highlight.gameObject.SetActive(true);

        while (true)
        {
            // Fade In
            float elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float newAlpha = Mathf.Lerp(0f, 1f, elapsedTime / fadeDuration);
                Color newColor = Highlight.color;
                newColor.a = newAlpha;
                Highlight.color = newColor;
                yield return null;
            }

            // Fade Out
            elapsedTime = 0f;
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float newAlpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeDuration);
                Color newColor = Highlight.color;
                newColor.a = newAlpha;
                Highlight.color = newColor;
                yield return null;
            }
        }
    }



    public void OnTurnBegin()
    {
        foreach (Player player in PlayerManager.Instance.Players)
        {
            if(TurnManager.Instance.ActivePlayer==player)
                player.Board.HighlightBoard();
            else
                player.Board.UnHighlightBoard();

        }   
        
    }
    public void OnTurnEnd()
    {
        foreach (Player player in PlayerManager.Instance.Players)
        {
            if (TurnManager.Instance.ActivePlayer == player)
                player.Board.HighlightBoard();
            else
                player.Board.UnHighlightBoard();

        }
    }
}
