using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PlayerSetupUI : MonoBehaviour
{
    private PlayerManager manager;
    private readonly List<PlayerSetupEntry> entries = new List<PlayerSetupEntry>();
    private readonly List<BlockedCanvas> blockedCanvases = new List<BlockedCanvas>();
    private RectTransform root;
    private RectTransform rows;
    private TMP_Text feedback;
    private Button addButton;
    private Button startButton;

    public IReadOnlyList<PlayerSetupEntry> Entries => entries;

    public void Initialize(PlayerManager playerManager)
    {
        manager = playerManager;
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

        root = Rect("Player Setup Canvas", transform);
        Canvas overlay = root.gameObject.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 1000;
        CanvasScaler scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        root.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.05f, 0.075f, 1f);

        RectTransform panel = Rect("Setup Panel", root);
        Stretch(panel);
        panel.offsetMin = new Vector2(80, 40);
        panel.offsetMax = new Vector2(-80, -40);
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 16;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        Text("Title", panel, "Set up your players", 44, 64);
        Text("Instructions", panel,
            $"Add {PlayerSetupRules.MinimumPlayers}-{manager.SupportedPlayerCount} players. " +
            "Enter different names and choose a different kingdom for each player.\n" +
            $"This scene has {manager.SupportedPlayerCount} configured boards. " +
            "Resource placement begins only after Start Game.", 26, 80);
        rows = Rect("Players", panel);
        VerticalLayoutGroup rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 16;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = false;
        rows.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
        feedback = Text("Validation", panel, "", 26, 58);
        feedback.color = new Color(1f, 0.78f, 0.42f);
        RectTransform footer = Rect("Setup Actions", panel);
        Horizontal(footer, 72);
        Button("Back to Menu", footer, () => SceneManager.LoadScene("mainMenu"));
        addButton = Button("Add Player", footer, AddPlayer);
        startButton = Button("Start Game", footer, StartGame);
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
        for (int i = rows.childCount - 1; i >= 0; i--)
        {
            GameObject child = rows.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerSetupEntry entry = entries[i];
            RectTransform row = Rect($"Player {i + 1} Setup", rows);
            Horizontal(row, 100);
            Text("Seat", row, $"{i + 1}.", 28, 64).GetComponent<LayoutElement>().flexibleWidth = 0;
            TMP_InputField nameInput = NameInput(row, entry.Name);
            nameInput.onValueChanged.AddListener(value =>
            {
                entry.Name = value;
                Validate();
            });
            TMP_Dropdown kingdoms = KingdomDropdown(row);
            kingdoms.options.Add(new TMP_Dropdown.OptionData("Choose a kingdom..."));
            if (manager.kingdomCatalog != null && manager.kingdomCatalog.kingdoms != null)
                foreach (Kingdom kingdom in manager.kingdomCatalog.kingdoms)
                    kingdoms.options.Add(new TMP_Dropdown.OptionData(kingdom != null ? kingdom.kingdomName : "Missing kingdom"));
            kingdoms.SetValueWithoutNotify(entry.Kingdom == null ? 0 :
                manager.kingdomCatalog.kingdoms.IndexOf(entry.Kingdom) + 1);
            kingdoms.RefreshShownValue();
            kingdoms.onValueChanged.AddListener(index =>
            {
                entry.Kingdom = index == 0 ? null : manager.kingdomCatalog.kingdoms[index - 1];
                Validate();
            });
            Button remove = Button("Remove Player", row, () => RemovePlayer(entry));
            remove.interactable = entries.Count > PlayerSetupRules.MinimumPlayers;
            remove.GetComponent<LayoutElement>().flexibleWidth = 0;
        }
        Validate();
    }

    private void Validate()
    {
        bool valid = PlayerSetupRules.Validate(entries, manager.kingdomCatalog, manager.SupportedPlayerCount, out string error);
        feedback.text = valid ? "Ready. Start Game to place your initial resources." : error;
        startButton.interactable = valid;
        addButton.interactable = entries.Count < manager.SupportedPlayerCount;
    }

    private static TMP_InputField NameInput(Transform parent, string value)
    {
        RectTransform rect = Rect("Player Name", parent);
        rect.gameObject.AddComponent<Image>().color = new Color(0.13f, 0.19f, 0.27f);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 300;
        size.flexibleWidth = 1;
        TMP_InputField input = rect.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = Rect("Text Area", rect);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(16, 8);
        viewport.offsetMax = new Vector2(-16, -8);
        viewport.gameObject.AddComponent<RectMask2D>();
        TMP_Text text = Text("Name", viewport, "", 28);
        Stretch(text.rectTransform);
        input.textViewport = viewport;
        input.textComponent = text;
        input.characterLimit = PlayerSetupRules.MaximumNameLength;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.text = value;
        return input;
    }

    private static TMP_Dropdown KingdomDropdown(Transform parent)
    {
        RectTransform rect = Rect("Kingdom", parent);
        Image background = rect.gameObject.AddComponent<Image>();
        background.color = new Color(0.13f, 0.19f, 0.27f);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 520;
        size.flexibleWidth = 2;
        TMP_Dropdown dropdown = rect.gameObject.AddComponent<TMP_Dropdown>();
        dropdown.targetGraphic = background;
        TMP_Text caption = Text("Selected Kingdom", rect, "", 28);
        Stretch(caption.rectTransform);
        caption.rectTransform.offsetMin = new Vector2(16, 0);
        caption.rectTransform.offsetMax = new Vector2(-40, 0);
        dropdown.captionText = caption;
        TMP_Text arrow = Text("Expand", rect, "v", 24);
        arrow.rectTransform.anchorMin = new Vector2(1, 0);
        arrow.rectTransform.anchorMax = Vector2.one;
        arrow.rectTransform.pivot = new Vector2(1, 0.5f);
        arrow.rectTransform.sizeDelta = new Vector2(36, 0);

        RectTransform template = Rect("Template", rect);
        template.anchorMin = Vector2.zero;
        template.anchorMax = new Vector2(1, 0);
        template.pivot = new Vector2(0.5f, 1);
        template.sizeDelta = new Vector2(0, 340);
        template.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.13f, 0.19f);
        ScrollRect scroll = template.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        RectTransform viewport = Rect("Viewport", template);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1);
        content.sizeDelta = new Vector2(0, 52);
        scroll.viewport = viewport;
        scroll.content = content;
        RectTransform item = Rect("Item", content);
        item.anchorMin = new Vector2(0, 0.5f);
        item.anchorMax = new Vector2(1, 0.5f);
        item.sizeDelta = new Vector2(0, 52);
        Image itemBackground = item.gameObject.AddComponent<Image>();
        itemBackground.color = new Color(0.16f, 0.23f, 0.32f);
        Toggle toggle = item.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        TMP_Text label = Text("Kingdom Name", item, "", 26);
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(18, 0);
        dropdown.itemText = label;
        dropdown.template = template;
        template.gameObject.SetActive(false);
        return dropdown;
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

    private static RectTransform Rect(string objectName, Transform parent)
    {
        var child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Horizontal(RectTransform rect, float height)
    {
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
    }

    private static TMP_Text Text(string objectName, Transform parent, string value, float fontSize, float height = 0)
    {
        TextMeshProUGUI text = Rect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.text = value;
        text.richText = false;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        if (height > 0)
        {
            LayoutElement size = text.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = height;
            size.preferredWidth = 60;
        }
        return text;
    }

    private static Button Button(string label, Transform parent, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = Rect(label, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.2f, 0.35f, 0.47f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        LayoutElement size = rect.gameObject.AddComponent<LayoutElement>();
        size.minWidth = 210;
        size.flexibleWidth = 1;
        TMP_Text text = Text("Label", rect, label, 28);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Center;
        return button;
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
