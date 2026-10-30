using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Drives GameScene's authored player setup screen: one card per player with their name, whether a human
/// or a bot plays it, kingdom (and what its power does), board theme and piece colour. The cards, buttons and layout are
/// scene objects; this only fills them in, shows a card per player and blocks the game until Start Game.
/// </summary>
public class PlayerSetupUI : MonoBehaviour
{
    [Tooltip("The setup canvas, inactive in the scene until setup begins.")]
    public GameObject screen;
    [Tooltip("One card per supported seat, in seat order.")]
    public List<PlayerSetupCard> cards = new List<PlayerSetupCard>();
    public UnityEngine.UI.Button addPlayerButton;
    public UnityEngine.UI.Button startButton;
    public UnityEngine.UI.Button backButton;
    public TMP_Text feedback;
    public Color readyColour = new Color32(0xE2, 0xB5, 0x4A, 0xFF);
    public Color pendingColour = new Color32(0xA5, 0xAE, 0xBA, 0xFF);

    private PlayerManager manager;
    private readonly List<PlayerSetupEntry> entries = new List<PlayerSetupEntry>();
    private readonly List<BlockedCanvas> blockedCanvases = new List<BlockedCanvas>();
    private bool initialized;

    public IReadOnlyList<PlayerSetupEntry> Entries => entries;

    private ThemeCatalog Themes => ThemeManager.Instance != null ? ThemeManager.Instance.catalog : null;
    private int Capacity => Mathf.Min(manager.SupportedPlayerCount, cards.Count);

    public void Initialize(PlayerManager playerManager)
    {
        if (initialized)
            return;
        initialized = true;
        manager = playerManager;
        Canvas own = screen != null ? screen.GetComponent<Canvas>() : null;
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == own)
                continue;
            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
            {
                // Every scene canvas carries an authored CanvasGroup; skip any that does not rather than add one.
                continue;
            }
            blockedCanvases.Add(new BlockedCanvas(group));
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        for (int i = 0; i < cards.Count; i++)
            Wire(cards[i], i);
        if (addPlayerButton != null)
            addPlayerButton.onClick.AddListener(AddPlayer);
        if (startButton != null)
            startButton.onClick.AddListener(StartGame);
        if (backButton != null)
            backButton.onClick.AddListener(() => SceneManager.LoadScene("mainMenu"));

        if (screen != null)
            screen.SetActive(true);
        for (int i = 0; i < Mathf.Min(PlayerSetupRules.MinimumPlayers, Capacity); i++)
            entries.Add(new PlayerSetupEntry($"Player {i + 1}"));
        RebuildRows();
    }

    public void AddPlayer()
    {
        if (entries.Count >= Capacity)
        {
            feedback.text = $"This scene supports at most {Capacity} players.";
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
        if (screen != null)
            screen.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        TurnManager.Instance.StartTurns();
        return true;
    }

    private void RebuildRows()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        ThemeCatalog themes = Themes;
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < entries.Count;
            cards[i].gameObject.SetActive(used);
            if (!used)
                continue;
            PlayerSetupEntry entry = entries[i];
            if (themes != null && themes.themes.Count > 0)
            {
                if (entry.Theme == null)
                    entry.Theme = themes.SeatDefault(i);
                if (entry.ColourIndex < 0)
                    entry.ColourIndex = FreeColour(themes, i);
            }
            Bind(cards[i], i, entry);
        }
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
        feedback.color = valid ? readyColour : pendingColour;
        startButton.interactable = valid;
        bool canAdd = entries.Count < Capacity;
        addPlayerButton.interactable = canAdd;
        addPlayerButton.gameObject.SetActive(canAdd);
    }

    // ------------------------------------------------------------------ cards

    private PlayerSetupEntry EntryAt(int seat) => seat < entries.Count ? entries[seat] : null;

    // Listeners are added once; each reads whichever entry the card shows at the time.
    private void Wire(PlayerSetupCard card, int seat)
    {
        card.removeButton.onClick.AddListener(() =>
        {
            PlayerSetupEntry entry = EntryAt(seat);
            if (entry != null)
                RemovePlayer(entry);
        });
        card.nameInput.onValueChanged.AddListener(value =>
        {
            PlayerSetupEntry entry = EntryAt(seat);
            if (entry == null)
                return;
            entry.Name = value;
            Validate();
        });
        card.controlButton.onClick.AddListener(() => ToggleBot(seat));
        card.previousTheme.onClick.AddListener(() => StepTheme(seat, -1));
        card.nextTheme.onClick.AddListener(() => StepTheme(seat, 1));
        card.pieceButton.onClick.AddListener(() =>
        {
            PlayerSetupEntry entry = EntryAt(seat);
            ThemeCatalog themes = Themes;
            if (entry == null || themes == null)
                return;
            entry.ColourIndex = themes.NextColour(entry.ColourIndex, 1);
            Bind(card, seat, entry);
        });
        for (int c = 0; c < card.swatches.Count; c++)
        {
            int index = c;
            card.swatches[c].onClick.AddListener(() =>
            {
                PlayerSetupEntry entry = EntryAt(seat);
                if (entry == null)
                    return;
                entry.ColourIndex = index;
                Bind(card, seat, entry);
            });
        }

        TMP_Dropdown kingdoms = card.kingdomDropdown;
        kingdoms.ClearOptions();
        var options = new List<string> { "Choose a kingdom..." };
        if (manager.kingdomCatalog != null && manager.kingdomCatalog.kingdoms != null)
            foreach (Kingdom kingdom in manager.kingdomCatalog.kingdoms)
                options.Add(KingdomLabel(kingdom));
        kingdoms.AddOptions(options);
        kingdoms.onValueChanged.AddListener(index =>
        {
            PlayerSetupEntry entry = EntryAt(seat);
            if (entry == null)
                return;
            entry.Kingdom = index == 0 ? null : manager.kingdomCatalog.kingdoms[index - 1];
            ShowKingdom(card, entry.Kingdom);
            Validate();
        });
    }

    // A bot plays its seat through BotPlayerController. Default names follow the switch ("Player 2" <-> "Bot 2"),
    // and a bot without a kingdom gets a free one at random so the game can start.
    private void ToggleBot(int seat)
    {
        PlayerSetupEntry entry = EntryAt(seat);
        if (entry == null)
            return;
        entry.IsBotControlled = !entry.IsBotControlled;
        string from = entry.IsBotControlled ? "Player " : "Bot ";
        string to = entry.IsBotControlled ? "Bot " : "Player ";
        if (entry.Name != null && entry.Name.StartsWith(from) && int.TryParse(entry.Name.Substring(from.Length), out int number) &&
            !entries.Exists(other => other != entry && other.Name == to + number))
            entry.Name = to + number;
        if (entry.IsBotControlled && entry.Kingdom == null && manager.kingdomCatalog != null)
        {
            List<Kingdom> free = manager.kingdomCatalog.kingdoms.FindAll(kingdom =>
                kingdom != null && !entries.Exists(other => other.Kingdom == kingdom));
            if (free.Count > 0)
                entry.Kingdom = free[Random.Range(0, free.Count)];
        }
        RebuildRows();
    }

    private void StepTheme(int seat, int direction)
    {
        PlayerSetupEntry entry = EntryAt(seat);
        ThemeCatalog themes = Themes;
        if (entry == null || themes == null)
            return;
        entry.Theme = themes.Next(entry.Theme, direction);
        Bind(cards[seat], seat, entry);
    }

    private void Bind(PlayerSetupCard card, int seat, PlayerSetupEntry entry)
    {
        ThemeCatalog themes = Themes;
        card.seatLabel.text = $"PLAYER {seat + 1}";
        card.removeButton.interactable = entries.Count > PlayerSetupRules.MinimumPlayers;
        card.nameInput.SetTextWithoutNotify(entry.Name);
        card.controlLabel.text = entry.IsBotControlled ? "Bot" : "Human";
        TMP_Dropdown kingdoms = card.kingdomDropdown;
        kingdoms.SetValueWithoutNotify(entry.Kingdom == null || manager.kingdomCatalog == null ? 0 :
            manager.kingdomCatalog.kingdoms.IndexOf(entry.Kingdom) + 1);
        kingdoms.RefreshShownValue();
        ShowKingdom(card, entry.Kingdom);

        PlayerColour colour = themes != null && themes.usePlayerColours ? themes.ColourAt(entry.ColourIndex) : null;
        Color tone = colour != null ? colour.colour : readyColour;
        card.colourBar.color = tone;
        card.colourDot.color = tone;
        card.boardPreview.sprite = entry.Theme != null ? entry.Theme.board : null;
        card.boardPreview.enabled = card.boardPreview.sprite != null;
        // The piece shows the theme's layers in this player's colour, exactly as it will look on the board.
        card.piecePreview.sprite = ThemeManager.Instance != null
            ? ThemeManager.Instance.PieceSprite(entry.Theme, ResourceType.Water, entry.ColourIndex) : null;
        card.piecePreview.enabled = card.piecePreview.sprite != null;
        card.pieceButton.interactable = themes != null && themes.usePlayerColours && themes.playerColours.Count > 1;
        card.themeName.text = entry.Theme != null ? entry.Theme.DisplayName : "No theme";
        string era = entry.Theme != null ? entry.Theme.EraLabel : "";
        card.themeDetail.text = colour != null
            ? (era.Length > 0 ? $"{era}  ·  {colour.name} pieces" : $"{colour.name} pieces")
            : era;
        for (int c = 0; c < card.swatchRings.Count; c++)
        {
            bool exists = themes != null && c < themes.playerColours.Count;
            card.swatches[c].gameObject.SetActive(exists && themes.usePlayerColours);
            if (exists)
                card.swatches[c].targetGraphic.color = themes.playerColours[c].colour;
            card.swatchRings[c].enabled = colour != null && c == entry.ColourIndex % themes.playerColours.Count;
        }
    }

    private static void ShowKingdom(PlayerSetupCard card, Kingdom kingdom)
    {
        card.winCondition.SetActive(false);
        if (kingdom == null)
        {
            card.powerName.text = "No kingdom yet";
            card.powerDescription.text = "Pick a kingdom to see its power and the virtues it needs to win.";
            return;
        }
        if (kingdom.power != null)
        {
            int cost = kingdom.power.virtueCost;
            card.powerName.text = $"{kingdom.power.powerName}  ·  {cost} {(cost == 1 ? "virtue" : "virtues")}";
            card.powerDescription.text = kingdom.power.powerDescription;
        }
        else
        {
            card.powerName.text = kingdom.kingdomName;
            card.powerDescription.text = kingdom.kingdomStory;
        }
        int shown = 0;
        if (kingdom.virtuesForWin != null)
        {
            foreach (Kingdom.VirtuesForCost need in kingdom.virtuesForWin)
            {
                if (need == null || need.virtues == null || shown >= card.goalIcons.Count)
                    continue;
                card.goalIcons[shown].sprite = need.virtues.virtueIcon;
                card.goalCounts[shown].text = $"×{need.NumberofVirtues}";
                shown++;
            }
        }
        for (int i = 0; i < card.goalIcons.Count; i++)
        {
            card.goalIcons[i].gameObject.SetActive(i < shown);
            card.goalCounts[i].gameObject.SetActive(i < shown);
        }
        card.winCondition.SetActive(shown > 0);
    }

    private static string KingdomLabel(Kingdom kingdom)
    {
        if (kingdom == null)
            return "Missing kingdom";
        return kingdom.power != null && !string.IsNullOrEmpty(kingdom.power.powerName)
            ? $"{kingdom.kingdomName}  ·  {kingdom.power.powerName}"
            : kingdom.kingdomName;
    }

    private void RestoreCanvases()
    {
        foreach (BlockedCanvas state in blockedCanvases)
        {
            if (state.Group == null)
                continue;
            state.Group.interactable = state.Interactable;
            state.Group.blocksRaycasts = state.BlocksRaycasts;
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
        public readonly bool Interactable;
        public readonly bool BlocksRaycasts;

        public BlockedCanvas(CanvasGroup group)
        {
            Group = group;
            Interactable = group.interactable;
            BlocksRaycasts = group.blocksRaycasts;
        }
    }
}
