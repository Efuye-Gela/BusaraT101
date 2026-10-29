using Sirenix.Serialization;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PowerChoice
{
    public string Label;
    public Action OnSelected;
    public bool Enabled;
    public PowerOption Option;

    public PowerChoice(string label, Action onSelected, bool enabled = true, PowerOption option = null)
    {
        Label = label;
        OnSelected = onSelected;
        Enabled = enabled;
        Option = option;
    }
}

public class PowerUIManager : Manager<PowerUIManager>, TurnManager.TurnBeginListener
{
    private Player player;
    public List<Virtue> VirtuesList;
    public List<VirtueUI> VirtueUIList;
    [OdinSerialize]
    public Dictionary<VirtueType, int> virtueCounts = new Dictionary<VirtueType, int>();
    public TMP_Text powerInfo;
    public List<Transform> PowerPanelComponents;

    [Header("Power choices (authored in GameScene, inactive until needed)")]
    public Canvas choicesCanvas;
    public TMP_Text choicesTitle;
    public ScrollRect titleScroll;
    public RectTransform choicesContent;
    public ScrollRect choicesScroll;
    [Tooltip("Inactive template inside the choices content; each choice is a copy of it.")]
    public Button choiceTemplate;
    public Color choiceTextColour = Color.white;
    public Color disabledChoiceTextColour = new Color(0.6f, 0.63f, 0.67f, 1f);

    private TurnManager listeningTurnManager;
    private int choicesRevision;
    private bool selectingChoice;

    private void OnEnable()
    {
        ListenForTurns();
        RefreshPlayer();
    }

    private void OnDisable()
    {
        if (listeningTurnManager != null)
            listeningTurnManager.RemoveTurnBeginListener(this);
        listeningTurnManager = null;
        HideChoices();
    }

    private void Start()
    {
        ListenForTurns();
        RefreshPlayer();
    }

    private void ListenForTurns()
    {
        TurnManager turns = TurnManager.Instance;
        if (turns == listeningTurnManager)
            return;
        if (listeningTurnManager != null)
            listeningTurnManager.RemoveTurnBeginListener(this);
        listeningTurnManager = turns;
        if (listeningTurnManager != null)
            listeningTurnManager.AddTurnBeginListeners(this);
    }

    private void RefreshPlayer()
    {
        player = TurnManager.Instance != null ? TurnManager.Instance.ActivePlayer : null;
        VirtueDisplay();
        GetPlayerVirtueCount();
    }

    private void SetLegacyPanelActive(int index, bool active)
    {
        if (PowerPanelComponents != null && index < PowerPanelComponents.Count &&
            PowerPanelComponents[index] != null)
            PowerPanelComponents[index].gameObject.SetActive(active);
    }

    public void PowerPanelLayout()
    {
        SetLegacyPanelActive(0, true);
        SetLegacyPanelActive(1, true);
        SetLegacyPanelActive(2, false);
    }

    public void VirtueDisplay()
    {
        if (VirtuesList == null || VirtueUIList == null)
            return;
        for (int i = 0; i < Mathf.Min(VirtuesList.Count, VirtueUIList.Count); i++)
        {
            VirtueUI ui = VirtueUIList[i];
            Virtue virtue = VirtuesList[i];
            if (ui == null || virtue == null)
                continue;
            ui.virtueType = virtue;
            if (ui.VirtueName != null)
                ui.VirtueName.text = virtue.name;
            if (ui.virtueImage != null)
                ui.virtueImage.sprite = virtue.virtueIcon;
            ui.currentPlayer = player;
        }
    }

    public void GetPlayerVirtueCount()
    {
        if (virtueCounts == null)
            virtueCounts = new Dictionary<VirtueType, int>();
        foreach (VirtueType type in Enum.GetValues(typeof(VirtueType)))
        {
            virtueCounts[type] = 0;
        }

        if (player != null && player.Virtues != null)
        {
            foreach (Virtue virtue in player.Virtues)
            {
                if (virtue != null && virtueCounts.ContainsKey(virtue.type))
                    virtueCounts[virtue.type]++;
            }
        }

        if (VirtueUIList == null)
            return;
        foreach (VirtueUI virtueUI in VirtueUIList)
        {
            if (virtueUI != null && virtueUI.virtueType != null && virtueUI.NumberOfvirtues != null)
            {
                VirtueType virtueType = virtueUI.virtueType.type;
                virtueUI.NumberOfvirtues.text = virtueCounts.TryGetValue(virtueType, out int count)
                    ? count.ToString() : "0";
            }
        }
    }

    public void OnTurnBegin()
    {
        RefreshPlayer();
    }

    public void PowerMassage(string logString)
    {
        if (powerInfo != null)
        {
            powerInfo.gameObject.SetActive(true);
            powerInfo.text = logString ?? string.Empty;
        }
    }

    public void ShowChoices(string title, List<PowerChoice> choices)
    {
        SetLegacyPanelActive(0, false);
        if (choicesCanvas == null || choicesContent == null || choiceTemplate == null)
        {
            Debug.LogError("PowerUIManager needs its authored choices canvas, content and choice template assigned.");
            return;
        }
        ClearChoiceButtons();
        // Draw above every other canvas, including ones opened since the scene loaded.
        int sortingOrder = 100;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas != choicesCanvas)
                sortingOrder = Mathf.Max(sortingOrder, canvas.sortingOrder + 1);
        }
        choicesCanvas.sortingOrder = sortingOrder;
        int revision = ++choicesRevision;
        choicesTitle.text = title ?? string.Empty;
        choicesCanvas.gameObject.SetActive(true);

        if (choices != null)
        {
            foreach (PowerChoice choice in choices)
            {
                if (choice == null)
                    continue;
                CreateChoiceButton(choice, revision);
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(choicesTitle.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(choicesContent);
        titleScroll.StopMovement();
        titleScroll.verticalNormalizedPosition = 1f;
        choicesScroll.StopMovement();
        choicesScroll.verticalNormalizedPosition = 1f;
    }

    public void HideChoices()
    {
        ++choicesRevision;
        if (choicesCanvas != null)
        {
            choicesCanvas.gameObject.SetActive(false);
            ClearChoiceButtons();
        }
    }

    private void ClearChoiceButtons()
    {
        if (choicesContent == null)
            return;
        for (int i = choicesContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = choicesContent.GetChild(i).gameObject;
            if (choiceTemplate != null && child == choiceTemplate.gameObject)
                continue;
            Button button = child.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void CreateChoiceButton(PowerChoice choice, int revision)
    {
        Button button = Instantiate(choiceTemplate, choicesContent);
        button.name = "Choice";
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.interactable = choice.Enabled && choice.OnSelected != null;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = choice.Label ?? string.Empty;
            label.color = button.interactable ? choiceTextColour : disabledChoiceTextColour;
        }
        Action callback = choice.OnSelected;
        button.onClick.AddListener(() =>
        {
            if (revision != choicesRevision || selectingChoice || !isActiveAndEnabled ||
                !choicesCanvas.gameObject.activeInHierarchy || !button.interactable)
                return;

            // A selection may synchronously replace or close the entire menu.
            selectingChoice = true;
            button.interactable = false;
            try
            {
                callback?.Invoke();
            }
            finally
            {
                selectingChoice = false;
                if (revision == choicesRevision && button != null)
                    button.interactable = choice.Enabled && callback != null;
            }
        });
    }
}
