using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The pre-game screen: one card per player with their name, kingdom (and what its power does),
/// board theme and piece colour. Built in code over the game scene, which stays blocked until Start Game.
/// </summary>
public class PlayerSetupUI : MonoBehaviour
{
    private const float CardWidth = 420f;

    private PlayerManager manager;
    private UiKit ui;
    private readonly List<PlayerSetupEntry> entries = new List<PlayerSetupEntry>();
    private readonly List<BlockedCanvas> blockedCanvases = new List<BlockedCanvas>();
    private RectTransform root;
    private RectTransform cards;
    private RectTransform addCard;
    private TMP_Text feedback;
    private Button addButton;
    private Button startButton;

    public IReadOnlyList<PlayerSetupEntry> Entries => entries;

    private ThemeCatalog Themes => ThemeManager.Instance != null ? ThemeManager.Instance.catalog : null;

    public void Initialize(PlayerManager playerManager)
    {
        manager = playerManager;
        ui = new UiKit(manager.uiStyle);
        BusaraUiStyle style = ui.Style;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            bool added = group == null;
            if (added)
                group = canvas.gameObject.AddComponent<CanvasGroup>();
            blockedCanvases.Add(new BlockedCanvas(group, added));
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        root = UiKit.Rect("Player Setup Canvas", transform);
        Canvas overlay = root.gameObject.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 1000;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<Image>().color = style.backdrop;
        Backdrop(root, style);

        RectTransform page = UiKit.Rect("Setup Page", root);
        UiKit.Stretch(page);
        page.offsetMin = new Vector2(72, 36);
        page.offsetMax = new Vector2(-72, -36);
        UiKit.Vertical(page, 24);

        RectTransform header = UiKit.Rect("Header", page);
        UiKit.Size(header, preferredHeight: 112);
        UiKit.Horizontal(header, 32);
        if (style.logo != null)
        {
            Image logo = UiKit.Rect("Busara Logo", header).gameObject.AddComponent<Image>();
            logo.sprite = style.logo;
            logo.color = style.ink;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            UiKit.Size(logo, preferredWidth: 150, preferredHeight: 104);
        }
        RectTransform titles = UiKit.Rect("Titles", header);
        UiKit.Size(titles, flexibleWidth: 1, preferredHeight: 112);
        UiKit.Vertical(titles, 4, 0, TextAnchor.MiddleLeft);
        UiKit.Size(ui.Text("Title", titles, "Set up your players", style.Display, 50, style.ink), preferredHeight: 62);
        UiKit.Size(ui.Text("Instructions", titles,
            $"{PlayerSetupRules.MinimumPlayers} to {manager.SupportedPlayerCount} players. Give each player a different name and kingdom, " +
            "then make the board their own with a theme and a piece colour.", style.Body, 22, style.inkMuted), preferredHeight: 34);

        cards = UiKit.Rect("Players", page);
        UiKit.Size(cards, flexibleWidth: 1, flexibleHeight: 1, minHeight: 560);
        HorizontalLayoutGroup row = UiKit.Horizontal(cards, 24, 0, TextAnchor.UpperCenter);
        row.childForceExpandHeight = true;
        BuildAddCard();

        RectTransform footer = UiKit.Rect("Setup Actions", page);
        UiKit.Size(footer, preferredHeight: 80);
        HorizontalLayoutGroup footerRow = UiKit.Horizontal(footer, 24);
        footerRow.childForceExpandHeight = true;
        Button back = ui.Button("Back to Menu", footer, "Back to Menu", UiButtonKind.Ghost, 26);
        back.onClick.AddListener(() => SceneManager.LoadScene("mainMenu"));
        UiKit.Size(back, preferredWidth: 280);
        feedback = ui.Text("Validation", footer, "", style.Body, 24, style.inkMuted, TextAlignmentOptions.Center);
        UiKit.Size(feedback, flexibleWidth: 1);
        startButton = ui.Button("Start Game", footer, "Start Game", UiButtonKind.Primary, 30);
        startButton.onClick.AddListener(StartGame);
        UiKit.Size(startButton, preferredWidth: 360);

        for (int i = 0; i < Mathf.Min(PlayerSetupRules.MinimumPlayers, manager.SupportedPlayerCount); i++)
            entries.Add(new PlayerSetupEntry($"Player {i + 1}"));
        RebuildRows();
    }

    public void AddPlayer()
    {
        if (entries.Count >= manager.SupportedPlayerCount)
        {
            feedback.text = $"This scene supports at most {manager.SupportedPlayerCount} players.";
            return;
        }
        int number = 1;
        while (entries.Exists(entry => entry.Name == $"Player {number}"))
            number++;
        entries.Add(new PlayerSetupEntry($"Player {number}"));
        RebuildRows();
    }

    public void RemovePlayer(PlayerSetupEntry entry)
    {
        if (entries.Count <= PlayerSetupRules.MinimumPlayers)
        {
            feedback.text = "At least two players are required.";
            return;
        }
        if (!entries.Remove(entry))
        {
            feedback.text = "That player is no longer in this setup.";
            return;
        }
        RebuildRows();
    }

    public void StartGame()
    {
        if (!TryStartGame(entries, out string error))
            feedback.text = error;
    }

    public bool TryStartGame(IReadOnlyList<PlayerSetupEntry> players, out string error)
    {
        if (!manager.TryConfigurePlayers(players, out error))
        {
            return false;
        }
        RestoreCanvases();
        root.gameObject.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        TurnManager.Instance.StartTurns();
        return true;
    }

    private void RebuildRows()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        for (int i = cards.childCount - 1; i >= 0; i--)
        {
            GameObject child = cards.GetChild(i).gameObject;
            if (child == addCard.gameObject)
                continue;
            child.SetActive(false);
            Destroy(child);
        }
        ThemeCatalog themes = Themes;
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerSetupEntry entry = entries[i];
            if (themes != null && themes.themes.Count > 0)
            {
                if (entry.Theme == null)
                    entry.Theme = themes.SeatDefault(i);
                if (entry.ColourIndex < 0)
                    entry.ColourIndex = FreeColour(themes, i);
            }
            PlayerCard(i, entry, themes);
        }
        addCard.SetAsLastSibling();
        Validate();
    }

    // The seat's own colour unless another player already took it, then the first free one.
    private int FreeColour(ThemeCatalog themes, int seat)
    {
        int count = Mathf.Max(1, themes.playerColours.Count);
        for (int step = 0; step < count; step++)
        {
            int index = (seat + step) % count;
            if (!entries.Exists(other => other.ColourIndex == index))
                return index;
        }
        return seat % count;
    }

    private void Validate()
    {
        bool valid = PlayerSetupRules.Validate(entries, manager.kingdomCatalog, manager.SupportedPlayerCount, out string error);
        feedback.text = valid ? "Ready. Start Game to place your first resources." : error;
        feedback.color = valid ? ui.Style.accent : ui.Style.inkMuted;
        startButton.interactable = valid;
        bool canAdd = entries.Count < manager.SupportedPlayerCount;
        addButton.interactable = canAdd;
        addCard.gameObject.SetActive(canAdd);
    }

    // ------------------------------------------------------------------ cards

    private void PlayerCard(int seat, PlayerSetupEntry entry, ThemeCatalog themes)
    {
        BusaraUiStyle style = ui.Style;
        Image card = ui.Panel($"Player {seat + 1} Setup", cards, style.surface, 22);
        UiKit.Size(card, preferredWidth: CardWidth);
        UiKit.Vertical(card, 12, 22);

        // A thin bar along the top in the player's colour ties the card to their pieces on the board.
        Image bar = ui.Panel("Player Colour Bar", card.transform, style.accent, 3);
        bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 1f);
        barRect.anchorMax = Vector2.one;
        barRect.pivot = new Vector2(0.5f, 1f);
        barRect.sizeDelta = new Vector2(-88, 6);
        barRect.anchoredPosition = new Vector2(0, -8);

        // Header: seat and remove.
        RectTransform head = UiKit.Rect("Card Header", card.transform);
        UiKit.Size(head, preferredHeight: 40);
        UiKit.Horizontal(head, 10);
        Image dot = UiKit.Rect("Colour Dot", head).gameObject.AddComponent<Image>();
        dot.sprite = UiShapes.Circle;
        dot.raycastTarget = false;
        UiKit.Size(dot, preferredWidth: 16, preferredHeight: 16);
        TMP_Text seatLabel = ui.Text("Seat", head, $"PLAYER {seat + 1}", style.Heading, 18, style.inkMuted);
        seatLabel.characterSpacing = 8;
        UiKit.Size(seatLabel, flexibleWidth: 1, preferredHeight: 40);
        Button remove = ui.Button("Remove Player", head, "×", UiButtonKind.Icon, 30);
        remove.onClick.AddListener(() => RemovePlayer(entry));
        remove.interactable = entries.Count > PlayerSetupRules.MinimumPlayers;
        UiKit.Size(remove, preferredWidth: 40, preferredHeight: 40);

        TMP_InputField nameInput = NameInput(card.transform, entry.Name);
        nameInput.onValueChanged.AddListener(value =>
        {
            entry.Name = value;
            Validate();
        });

        // Board theme: large preview with the player's piece on it, arrows to change theme.
        Image board = null, piece = null;
        TMP_Text themeName = null, themeDetail = null;
        var swatches = new List<Image>();
        if (themes != null && themes.themes.Count > 0)
        {
            RectTransform preview = UiKit.Rect("Board Theme", card.transform);
            UiKit.Size(preview, minHeight: 150, flexibleHeight: 1);
            board = UiKit.Rect("Theme Preview", preview).gameObject.AddComponent<Image>();
            board.preserveAspect = true;
            board.raycastTarget = false;
            UiKit.Stretch(board.rectTransform);
            board.rectTransform.offsetMin = new Vector2(44, 0);
            board.rectTransform.offsetMax = new Vector2(-44, 0);

            RectTransform pieceRect = UiKit.Rect("Piece Colour", preview);
            UiKit.Pin(pieceRect, new Vector2(0.5f, 0f), new Vector2(92, 118), new Vector2(0, -6));
            piece = pieceRect.gameObject.AddComponent<Image>();
            piece.preserveAspect = true;
            Button pieceButton = pieceRect.gameObject.AddComponent<Button>();
            pieceButton.targetGraphic = piece;
            pieceButton.interactable = themes.usePlayerColours && themes.playerColours.Count > 1;
            pieceButton.onClick.AddListener(() =>
            {
                entry.ColourIndex = themes.NextColour(entry.ColourIndex, 1);
                Refresh();
            });

            Button previous = ui.Button("Previous Board Theme", preview, "‹", UiButtonKind.Icon, 34);
            UiKit.Pin(previous.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(44, 44), Vector2.zero);
            previous.onClick.AddListener(() => StepTheme(-1));
            Button next = ui.Button("Next Board Theme", preview, "›", UiButtonKind.Icon, 34);
            UiKit.Pin(next.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(44, 44), Vector2.zero);
            next.onClick.AddListener(() => StepTheme(1));

            themeName = ui.Text("Theme Name", card.transform, "", style.Heading, 24, style.ink, TextAlignmentOptions.Center);
            themeName.enableWordWrapping = false;
            UiKit.Size(themeName, preferredHeight: 30);
            themeDetail = ui.Text("Theme Detail", card.transform, "", style.Body, 18, style.inkMuted, TextAlignmentOptions.Center);
            UiKit.Size(themeDetail, preferredHeight: 22);

            if (themes.usePlayerColours && themes.playerColours.Count > 0)
            {
                RectTransform swatchRow = UiKit.Rect("Piece Colours", card.transform);
                UiKit.Size(swatchRow, preferredHeight: 34);
                UiKit.Horizontal(swatchRow, 9, 0, TextAnchor.MiddleCenter);
                for (int c = 0; c < themes.playerColours.Count; c++)
                {
                    int index = c;
                    RectTransform swatchRect = UiKit.Rect("Colour Swatch", swatchRow);
                    UiKit.Size(swatchRect, preferredWidth: 30, preferredHeight: 30);
                    Image swatch = swatchRect.gameObject.AddComponent<Image>();
                    swatch.sprite = UiShapes.Circle;
                    swatch.color = themes.playerColours[c].colour;
                    Button pick = swatchRect.gameObject.AddComponent<Button>();
                    pick.targetGraphic = swatch;
                    pick.transition = UnityEngine.UI.Selectable.Transition.None;
                    pick.onClick.AddListener(() =>
                    {
                        entry.ColourIndex = index;
                        Refresh();
                    });
                    Image ring = UiKit.Rect("Selected", swatchRect).gameObject.AddComponent<Image>();
                    ring.sprite = UiShapes.Ring;
                    ring.color = style.ink;
                    ring.raycastTarget = false;
                    UiKit.Stretch(ring.rectTransform, -5);
                    swatches.Add(ring);
                }
            }
        }

        // Kingdom: choice plus what its power does and what it needs to win.
        ui.Caption("Kingdom Caption", card.transform, "Kingdom");
        TMP_Dropdown kingdoms = KingdomDropdown(card.transform);
        kingdoms.options.Add(new TMP_Dropdown.OptionData("Choose a kingdom..."));
        if (manager.kingdomCatalog != null && manager.kingdomCatalog.kingdoms != null)
            foreach (Kingdom kingdom in manager.kingdomCatalog.kingdoms)
                kingdoms.options.Add(new TMP_Dropdown.OptionData(KingdomLabel(kingdom)));
        kingdoms.SetValueWithoutNotify(entry.Kingdom == null ? 0 :
            manager.kingdomCatalog.kingdoms.IndexOf(entry.Kingdom) + 1);
        kingdoms.RefreshShownValue();
        KingdomInfo info = KingdomInfoPanel(card.transform);
        info.Show(entry.Kingdom, ui);
        kingdoms.onValueChanged.AddListener(index =>
        {
            entry.Kingdom = index == 0 ? null : manager.kingdomCatalog.kingdoms[index - 1];
            info.Show(entry.Kingdom, ui);
            Validate();
        });

        void StepTheme(int direction)
        {
            entry.Theme = themes.Next(entry.Theme, direction);
            Refresh();
        }

        void Refresh()
        {
            PlayerColour colour = themes != null && themes.usePlayerColours ? themes.ColourAt(entry.ColourIndex) : null;
            Color tone = colour != null ? colour.colour : style.accent;
            bar.color = tone;
            dot.color = tone;
            if (board == null)
                return;
            board.sprite = entry.Theme != null ? entry.Theme.board : null;
            board.color = board.sprite != null ? Color.white : Color.clear;
            // The piece shows the theme's layers in this player's colour, exactly as it will look on the board.
            piece.sprite = ThemeManager.Instance != null ? ThemeManager.Instance.PieceSprite(entry.Theme, ResourceType.Water, entry.ColourIndex) : null;
            piece.color = piece.sprite != null ? Color.white : Color.clear;
            themeName.text = entry.Theme != null ? entry.Theme.DisplayName : "No theme";
            string era = entry.Theme != null ? entry.Theme.EraLabel : "";
            themeDetail.text = colour != null ? (era.Length > 0 ? $"{era}  ·  {colour.name} pieces" : $"{colour.name} pieces") : era;
            for (int c = 0; c < swatches.Count; c++)
                swatches[c].enabled = colour != null && c == entry.ColourIndex % themes.playerColours.Count;
        }
        Refresh();
    }

    private void BuildAddCard()
    {
        BusaraUiStyle style = ui.Style;
        Image outline = ui.Panel("Add Player", cards, style.line, 22, true);
        addCard = outline.rectTransform;
        UiKit.Size(outline, preferredWidth: CardWidth);
        addButton = outline.gameObject.AddComponent<Button>();
        outline.raycastTarget = true;
        addButton.targetGraphic = outline;
        UiKit.Tint(addButton, style.line);
        addButton.onClick.AddListener(AddPlayer);
        RectTransform content = UiKit.Rect("Content", addCard);
        UiKit.Pin(content, new Vector2(0.5f, 0.5f), new Vector2(300, 200), Vector2.zero);
        UiKit.Vertical(content, 8, 0, TextAnchor.MiddleCenter);
        UiKit.Size(ui.Text("Plus", content, "+", style.Body, 84, style.inkMuted, TextAlignmentOptions.Center), preferredHeight: 100);
        UiKit.Size(ui.Text("Label", content, "Add player", style.Heading, 26, style.ink, TextAlignmentOptions.Center), preferredHeight: 34);
        UiKit.Size(ui.Text("Hint", content, $"Up to {manager.SupportedPlayerCount} kingdoms", style.Body, 18, style.inkMuted,
            TextAlignmentOptions.Center), preferredHeight: 24);
    }

    private static string KingdomLabel(Kingdom kingdom)
    {
        if (kingdom == null)
            return "Missing kingdom";
        return kingdom.power != null && !string.IsNullOrEmpty(kingdom.power.powerName)
            ? $"{kingdom.kingdomName}  ·  {kingdom.power.powerName}"
            : kingdom.kingdomName;
    }

    private TMP_InputField NameInput(Transform parent, string value)
    {
        BusaraUiStyle style = ui.Style;
        Image field = ui.Panel("Player Name", parent, style.raised, 14);
        field.raycastTarget = true;
        UiKit.Size(field, preferredHeight: 58);
        TMP_InputField input = field.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = field;
        UiKit.Tint(input, style.raised);
        RectTransform viewport = UiKit.Rect("Text Area", field.transform);
        UiKit.Stretch(viewport);
        viewport.offsetMin = new Vector2(18, 6);
        viewport.offsetMax = new Vector2(-18, -6);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text placeholder = ui.Text("Placeholder", viewport, "Player name", style.Body, 26, style.inkMuted);
        placeholder.enableWordWrapping = false;
        UiKit.Stretch(placeholder.rectTransform);
        TMP_Text text = ui.Text("Name", viewport, "", style.Heading, 26, style.ink);
        text.enableWordWrapping = false;
        UiKit.Stretch(text.rectTransform);
        input.textViewport = viewport;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = PlayerSetupRules.MaximumNameLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.customCaretColor = true;
        input.caretColor = style.accent;
        input.selectionColor = new Color(style.accent.r, style.accent.g, style.accent.b, 0.35f);
        input.text = value;
        return input;
    }

    private TMP_Dropdown KingdomDropdown(Transform parent)
    {
        BusaraUiStyle style = ui.Style;
        Image background = ui.Panel("Kingdom", parent, style.raised, 14);
        background.raycastTarget = true;
        UiKit.Size(background, preferredHeight: 58);
        TMP_Dropdown dropdown = background.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        UiKit.Tint(dropdown, style.raised);
        TMP_Text caption = ui.Text("Selected Kingdom", background.transform, "", style.Heading, 24, style.ink);
        caption.enableWordWrapping = false;
        UiKit.Stretch(caption.rectTransform);
        caption.rectTransform.offsetMin = new Vector2(18, 0);
        caption.rectTransform.offsetMax = new Vector2(-48, 0);
        dropdown.captionText = caption;
        // A right-pointing guillemet turned to point down: every font in the project has it.
        TMP_Text arrow = ui.Text("Expand", background.transform, "›", style.Body, 34, style.inkMuted, TextAlignmentOptions.Center);
        arrow.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
        arrow.rectTransform.anchorMin = new Vector2(1, 0);
        arrow.rectTransform.anchorMax = Vector2.one;
        arrow.rectTransform.pivot = new Vector2(1, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(48, 0);

        Image template = ui.Panel("Template", background.transform, style.raised, 14);
        template.raycastTarget = true;
        RectTransform templateRect = template.rectTransform;
        templateRect.anchorMin = Vector2.zero;
        templateRect.anchorMax = new Vector2(1, 0);
        templateRect.pivot = new Vector2(0.5f, 1);
        templateRect.anchoredPosition = new Vector2(0, -6);
        templateRect.sizeDelta = new Vector2(0, 380);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        RectTransform viewport = UiKit.Rect("Viewport", templateRect);
        UiKit.Stretch(viewport, 6);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = UiKit.Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = new Vector2(0, 48);
        scroll.viewport = viewport;
        scroll.content = content;
        RectTransform item = UiKit.Rect("Item", content);
        item.anchorMin = new Vector2(0, 0.5f);
        item.anchorMax = new Vector2(1, 0.5f);
        item.sizeDelta = new Vector2(0, 48);
        Image itemBackground = item.gameObject.AddComponent<Image>();
        UiKit.Round(itemBackground, 10);
        Toggle toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        UiKit.Tint(toggle, new Color(style.raised.r, style.raised.g, style.raised.b, 0f));
        ColorBlock itemColours = toggle.colors;
        itemColours.highlightedColor = style.line;
        itemColours.selectedColor = style.line;
        toggle.colors = itemColours;
        Image check = UiKit.Rect("Item Checkmark", item).gameObject.AddComponent<Image>();
        check.sprite = UiShapes.Circle;
        check.color = style.accent;
        check.raycastTarget = false;
        UiKit.Pin(check.rectTransform, new Vector2(0f, 0.5f), new Vector2(10, 10), new Vector2(14, 0));
        toggle.graphic = check;
        TMP_Text label = ui.Text("Kingdom Name", item, "", style.Body, 22, style.ink);
        label.enableWordWrapping = false;
        UiKit.Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(36, 0);
        dropdown.itemText = label;
        dropdown.template = templateRect;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private KingdomInfo KingdomInfoPanel(Transform parent)
    {
        BusaraUiStyle style = ui.Style;
        Image panel = ui.Panel("Kingdom Power", parent, style.raised, 14);
        UiKit.Size(panel, minHeight: 132, preferredHeight: 150);
        UiKit.Vertical(panel, 6, 14);
        var info = new KingdomInfo
        {
            Power = ui.Text("Power Name", panel.transform, "", style.Heading, 21, style.accent),
            Description = ui.Text("Power Description", panel.transform, "", style.Body, 17, style.inkMuted, TextAlignmentOptions.TopLeft),
            Goal = UiKit.Rect("Win Condition", panel.transform),
        };
        info.Power.enableWordWrapping = false;
        UiKit.Size(info.Power, preferredHeight: 26);
        UiKit.Size(info.Description, flexibleHeight: 1, minHeight: 44);
        UiKit.Size(info.Goal, preferredHeight: 30);
        UiKit.Horizontal(info.Goal, 6);
        return info;
    }

    /// <summary>The power and win condition of the chosen kingdom, shown under the kingdom choice.</summary>
    private sealed class KingdomInfo
    {
        public TMP_Text Power;
        public TMP_Text Description;
        public RectTransform Goal;

        public void Show(Kingdom kingdom, UiKit ui)
        {
            for (int i = Goal.childCount - 1; i >= 0; i--)
                Destroy(Goal.GetChild(i).gameObject);
            if (kingdom == null)
            {
                Power.text = "No kingdom yet";
                Power.color = ui.Style.inkMuted;
                Description.text = "Pick a kingdom to see its power and the virtues it needs to win.";
                return;
            }
            Power.color = ui.Style.accent;
            if (kingdom.power != null)
            {
                int cost = kingdom.power.virtueCost;
                Power.text = $"{kingdom.power.powerName}  ·  {cost} {(cost == 1 ? "virtue" : "virtues")}";
                Description.text = kingdom.power.powerDescription;
            }
            else
            {
                Power.text = kingdom.kingdomName;
                Description.text = kingdom.kingdomStory;
            }
            if (kingdom.virtuesForWin == null || kingdom.virtuesForWin.Length == 0)
                return;
            TMP_Text goal = ui.Text("To Win", Goal, "TO WIN", ui.Style.Heading, 15, ui.Style.inkMuted);
            goal.characterSpacing = 6;
            goal.enableWordWrapping = false;
            UiKit.Size(goal, preferredWidth: 70, preferredHeight: 30);
            foreach (Kingdom.VirtuesForCost need in kingdom.virtuesForWin)
            {
                if (need == null || need.virtues == null)
                    continue;
                Image icon = UiKit.Rect("Virtue", Goal).gameObject.AddComponent<Image>();
                icon.sprite = need.virtues.virtueIcon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                UiKit.Size(icon, preferredWidth: 26, preferredHeight: 26);
                TMP_Text count = ui.Text("Count", Goal, $"×{need.NumberofVirtues}", ui.Style.Heading, 18, ui.Style.ink);
                count.enableWordWrapping = false;
                UiKit.Size(count, preferredWidth: 34, preferredHeight: 30);
            }
        }
    }

    // ------------------------------------------------------------------ shared

    /// <summary>The menu backdrop art, filling the screen without stretching, under a dark wash.</summary>
    public static void Backdrop(RectTransform parent, BusaraUiStyle style)
    {
        if (style.background == null)
            return;
        Image art = UiKit.Rect("Backdrop", parent).gameObject.AddComponent<Image>();
        art.sprite = style.background;
        art.raycastTarget = false;
        art.rectTransform.anchorMin = art.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        AspectRatioFitter fit = art.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fit.aspectRatio = style.background.rect.width / Mathf.Max(1f, style.background.rect.height);
        Image wash = UiKit.Rect("Backdrop Wash", parent).gameObject.AddComponent<Image>();
        wash.color = new Color(style.backdrop.r, style.backdrop.g, style.backdrop.b, 0.78f);
        wash.raycastTarget = false;
        UiKit.Stretch(wash.rectTransform);
    }

    private void RestoreCanvases()
    {
        foreach (BlockedCanvas state in blockedCanvases)
        {
            if (state.Group == null)
                continue;
            state.Group.interactable = state.Interactable;
            state.Group.blocksRaycasts = state.BlocksRaycasts;
            if (state.Added)
                Destroy(state.Group);
        }
        blockedCanvases.Clear();
    }

    private void OnDestroy()
    {
        RestoreCanvases();
    }

    private sealed class BlockedCanvas
    {
        public readonly CanvasGroup Group;
        public readonly bool Added;
        public readonly bool Interactable;
        public readonly bool BlocksRaycasts;

        public BlockedCanvas(CanvasGroup group, bool added)
        {
            Group = group;
            Added = added;
            Interactable = group.interactable;
            BlocksRaycasts = group.blocksRaycasts;
        }
    }
}
