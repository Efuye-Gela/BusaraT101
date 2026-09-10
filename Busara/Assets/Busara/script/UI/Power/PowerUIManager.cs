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

    private TurnManager listeningTurnManager;
    private Canvas choicesCanvas;
    private TMP_Text choicesTitle;
    private ScrollRect titleScroll;
    private RectTransform choicesContent;
    private ScrollRect choicesScroll;
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
        EnsureChoicesCanvas();
        ClearChoiceButtons();
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
            Button button = child.GetComponent<Button>();
            if (button != null)
                button.onClick.RemoveAllListeners();
            child.SetActive(false);
            Destroy(child);
        }
    }

    private void EnsureChoicesCanvas()
    {
        if (choicesCanvas != null)
            return;

        RectTransform root = CreateRect("Power Choices Canvas", transform);
        choicesCanvas = root.gameObject.AddComponent<Canvas>();
        choicesCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        choicesCanvas.overrideSorting = true;
        int sortingOrder = 100;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas != choicesCanvas)
                sortingOrder = Mathf.Max(sortingOrder, canvas.sortingOrder + 1);
        }
        choicesCanvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        RectTransform panel = CreateRect("Choice Panel", root);
        Stretch(panel, new Vector2(0.52f, 0.04f), new Vector2(0.98f, 0.96f));
        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.055f, 0.07f, 0.1f, 0.98f);
        background.raycastTarget = true;
        VerticalLayoutGroup panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(24, 24, 20, 20);
        panelLayout.spacing = 16f;
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        RectTransform titleArea = CreateRect("Title Scroll", panel);
        LayoutElement titleLayout = titleArea.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 120f;
        titleLayout.preferredHeight = 190f;
        titleScroll = titleArea.gameObject.AddComponent<ScrollRect>();
        titleScroll.horizontal = false;
        titleScroll.vertical = true;
        titleScroll.movementType = ScrollRect.MovementType.Clamped;
        titleScroll.scrollSensitivity = 36f;
        RectTransform titleViewport = CreateRect("Viewport", titleArea);
        Stretch(titleViewport, Vector2.zero, Vector2.one);
        titleViewport.offsetMax = new Vector2(-22f, 0f);
        Image titleBackground = titleViewport.gameObject.AddComponent<Image>();
        titleBackground.color = new Color(0f, 0f, 0f, 0.04f);
        titleViewport.gameObject.AddComponent<RectMask2D>();
        titleScroll.viewport = titleViewport;
        choicesTitle = CreateText("Title", titleViewport, 28f);
        choicesTitle.fontStyle = FontStyles.Bold;
        choicesTitle.alignment = TextAlignmentOptions.TopLeft;
        RectTransform titleContent = choicesTitle.rectTransform;
        titleContent.anchorMin = new Vector2(0f, 1f);
        titleContent.anchorMax = Vector2.one;
        titleContent.pivot = new Vector2(0.5f, 1f);
        titleContent.sizeDelta = Vector2.zero;
        ContentSizeFitter titleFitter = titleContent.gameObject.AddComponent<ContentSizeFitter>();
        titleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        titleScroll.content = titleContent;
        AddScrollbar(titleArea, titleScroll);

        RectTransform scrollArea = CreateRect("Choices Scroll", panel);
        LayoutElement scrollLayout = scrollArea.gameObject.AddComponent<LayoutElement>();
        scrollLayout.minHeight = 100f;
        scrollLayout.flexibleHeight = 1f;
        choicesScroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        choicesScroll.horizontal = false;
        choicesScroll.vertical = true;
        choicesScroll.movementType = ScrollRect.MovementType.Clamped;
        choicesScroll.scrollSensitivity = 36f;

        RectTransform viewport = CreateRect("Viewport", scrollArea);
        Stretch(viewport, Vector2.zero, Vector2.one);
        viewport.offsetMax = new Vector2(-22f, 0f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.04f);
        viewport.gameObject.AddComponent<RectMask2D>();
        choicesScroll.viewport = viewport;

        choicesContent = CreateRect("Content", viewport);
        choicesContent.anchorMin = new Vector2(0f, 1f);
        choicesContent.anchorMax = Vector2.one;
        choicesContent.pivot = new Vector2(0.5f, 1f);
        choicesContent.sizeDelta = Vector2.zero;
        VerticalLayoutGroup contentLayout = choicesContent.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 12f;
        contentLayout.padding = new RectOffset(0, 0, 0, 8);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        ContentSizeFitter fitter = choicesContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        choicesScroll.content = choicesContent;
        AddScrollbar(scrollArea, choicesScroll);
    }

    private static void AddScrollbar(RectTransform scrollArea, ScrollRect scroll)
    {
        RectTransform scrollbarRect = CreateRect("Scrollbar", scrollArea);
        Stretch(scrollbarRect, new Vector2(1f, 0f), Vector2.one);
        scrollbarRect.offsetMin = new Vector2(-14f, 0f);
        Image track = scrollbarRect.gameObject.AddComponent<Image>();
        track.color = new Color(1f, 1f, 1f, 0.12f);
        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        RectTransform handle = CreateRect("Handle", scrollbarRect);
        Stretch(handle, Vector2.zero, Vector2.one);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.6f, 0.7f, 0.8f, 1f);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    private void CreateChoiceButton(PowerChoice choice, int revision)
    {
        RectTransform rect = CreateRect("Choice", choicesContent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.16f, 0.24f, 0.34f, 1f);
        colors.highlightedColor = new Color(0.24f, 0.37f, 0.51f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.1f, 0.17f, 0.25f, 1f);
        colors.disabledColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        button.colors = colors;
        button.interactable = choice.Enabled && choice.OnSelected != null;
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = 76f;
        VerticalLayoutGroup layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        TMP_Text label = CreateText("Label", rect, 26f);
        label.text = choice.Label ?? string.Empty;
        label.color = button.interactable ? Color.white : new Color(0.6f, 0.63f, 0.67f, 1f);
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

    private TMP_Text CreateText(string objectName, Transform parent, float fontSize)
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = powerInfo != null && powerInfo.font != null
            ? powerInfo.font : TMP_Settings.defaultFontAsset;
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = false;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
