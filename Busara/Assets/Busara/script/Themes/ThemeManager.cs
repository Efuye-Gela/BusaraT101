using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Applies a <see cref="BoardTheme"/> to each kingdom board: board art, turn glow, slot
/// geometry and overlays, and every resource piece that lands on that board.
/// Seats start on the catalog's defaults; player setup can pick a different theme per player.
/// </summary>
public class ThemeManager : Manager<ThemeManager>
{
    public ThemeCatalog catalog;

    private readonly Dictionary<Board, BoardTheme> applied = new Dictionary<Board, BoardTheme>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ApplySeatDefaults(false);
    }

    /// <summary>Gives every seat its default theme. With <paramref name="overwrite"/> false, boards
    /// that already have a theme (for example from player setup) keep it.</summary>
    public void ApplySeatDefaults(bool overwrite)
    {
        if (catalog == null)
            return;
        var seen = new HashSet<Board>();
        int seat = 0;
        if (PlayerManager.Instance != null && PlayerManager.Instance.Players != null)
        {
            foreach (Player player in PlayerManager.Instance.Players)
            {
                if (player != null && player.Board != null && seen.Add(player.Board) &&
                    (overwrite || !applied.ContainsKey(player.Board)))
                    Apply(player.Board, catalog.SeatDefault(seat));
                seat++;
            }
        }
        if (BoardManager.Instance != null && BoardManager.Instance.gameBoards != null)
        {
            foreach (Board board in BoardManager.Instance.gameBoards)
            {
                if (board != null && seen.Add(board) && (overwrite || !applied.ContainsKey(board)))
                    Apply(board, catalog.SeatDefault(seat));
                seat++;
            }
        }
    }

    public BoardTheme ThemeOf(Board board)
    {
        return board != null && applied.TryGetValue(board, out BoardTheme theme) ? theme : null;
    }

    public void Apply(Board board, BoardTheme theme)
    {
        if (board == null || theme == null)
            return;
        applied[board] = theme;

        Image background = board.Background != null ? board.Background : FindBackground(board);
        if (background != null)
        {
            background.sprite = theme.board;
            background.color = Color.white;
            background.preserveAspect = true;
        }
        if (board.Highlight != null)
        {
            // Board.FadeHighlight animates the alpha, so keep it.
            board.Highlight.sprite = theme.turnGlow;
            board.Highlight.color = new Color(1f, 1f, 1f, board.Highlight.color.a);
            Stretch(board.Highlight.rectTransform);
        }

        LayoutSlots(board);
        if (board.Slots == null)
            return;
        foreach (Slot slot in board.Slots)
        {
            if (slot == null)
                continue;
            SkinSlot(slot, theme);
            if (slot.resource != null)
                SkinResource(slot.resource);
        }
    }

    /// <summary>Re-skins a piece for whichever board it now sits on.</summary>
    public static void Skin(Resource resource)
    {
        ThemeManager manager = Instance;
        if (manager != null && resource != null)
            manager.SkinResource(resource);
    }

    public void SkinResource(Resource resource)
    {
        BoardTheme theme = ThemeFor(resource);
        if (theme == null)
            return;
        Image piece = resource.PieceImage;
        Sprite sprite = theme.PieceFor(resource.resourceType);
        if (piece != null && sprite != null)
        {
            piece.sprite = sprite;
            piece.preserveAspect = true;
        }
        Image glow = resource.HighlightImage;
        if (glow != null && theme.pieceSelected != null)
        {
            glow.sprite = theme.pieceSelected;
            glow.color = Color.white;
        }
    }

    private BoardTheme ThemeFor(Resource resource)
    {
        Board board = resource.slot != null ? resource.slot.board : null;
        if (board == null && TurnManager.Instance != null && TurnManager.Instance.ActivePlayer != null)
            board = TurnManager.Instance.ActivePlayer.Board;
        BoardTheme theme = ThemeOf(board);
        if (theme == null && catalog != null)
            theme = catalog.SeatDefault(0);
        return theme;
    }

    private static void LayoutSlots(Board board)
    {
        if (board.Slots == null || board.Slots.Count == 0 || board.Slots[0] == null)
            return;
        GridLayoutGroup grid = board.Slots[0].transform.parent != null
            ? board.Slots[0].transform.parent.GetComponent<GridLayoutGroup>()
            : null;
        if (grid == null)
            return;
        float width = BoardWidth(board);
        if (width <= 0f)
            return;
        float cell = width * BoardTheme.SlotPitch;
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = Vector2.zero;
    }

    private static float BoardWidth(Board board)
    {
        // The boards sit in a grid, so its cell size is reliable before the first layout pass.
        GridLayoutGroup boards = board.transform.parent != null ? board.transform.parent.GetComponent<GridLayoutGroup>() : null;
        if (boards != null && boards.cellSize.x > 0f)
            return boards.cellSize.x;
        return ((RectTransform)board.transform).rect.width;
    }

    private static void SkinSlot(Slot slot, BoardTheme theme)
    {
        // The board sprite draws the hole; the slot image stays as an invisible click target.
        Image hole = slot.GetComponent<Image>();
        if (hole != null)
            hole.color = new Color(1f, 1f, 1f, 0f);
        Image highlight = slot.HighlightImage;
        if (highlight != null && theme.slotLegal != null)
        {
            highlight.sprite = theme.slotLegal;
            highlight.color = Color.white;
            highlight.preserveAspect = true;
            Stretch(highlight.rectTransform);
        }
    }

    private static Image FindBackground(Board board)
    {
        foreach (Transform child in board.transform)
            if (child.name.StartsWith("Board Background"))
                return child.GetComponent<Image>();
        return null;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
