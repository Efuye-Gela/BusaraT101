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
    /// <summary>Piece diameter as a fraction of one slot, matching the art lab's composition.</summary>
    public const float PieceScale = 0.75f;

    public ThemeCatalog catalog;

    private readonly Dictionary<Board, BoardTheme> applied = new Dictionary<Board, BoardTheme>();
    // Size of each board's drawn art at the last layout, so slots follow resolution or layout changes.
    private readonly Dictionary<Board, Vector2> fittedSize = new Dictionary<Board, Vector2>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        ApplySeatDefaults(false);
    }

    private void LateUpdate()
    {
        // Layout groups size the boards after Start and again on resolution changes; refit when the art moves.
        foreach (Board board in applied.Keys)
        {
            if (board == null || !board.isActiveAndEnabled)
                continue;
            Vector2 size = DrawnArtSize(board);
            if (size.x > 0f && (!fittedSize.TryGetValue(board, out Vector2 last) || (last - size).sqrMagnitude > 0.01f))
                FitBoard(board);
        }
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

        if (board.Slots == null)
            return;
        foreach (Slot slot in board.Slots)
            if (slot != null)
                SkinSlot(slot, theme);
        FitBoard(board);
    }

    /// <summary>Lines the slot grid up with the holes in the board art and sizes every piece to its hole.</summary>
    public void FitBoard(Board board)
    {
        if (board == null || !LayoutSlots(board))
            return;
        fittedSize[board] = DrawnArtSize(board);
        foreach (Slot slot in board.Slots)
            if (slot != null && slot.resource != null)
                SkinResource(slot.resource);
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
        SizePiece(resource);
    }

    private static void SizePiece(Resource resource)
    {
        if (resource.slot == null || !(resource.transform is RectTransform rect))
            return;
        GridLayoutGroup grid = resource.slot.transform.parent != null
            ? resource.slot.transform.parent.GetComponent<GridLayoutGroup>()
            : null;
        if (grid == null || grid.cellSize.x <= 0f)
            return;
        // Centre on the slot whatever the prefab's anchors are, at a size relative to the hole.
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.sizeDelta = grid.cellSize * PieceScale;
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

    private static bool LayoutSlots(Board board)
    {
        if (board.Slots == null || board.Slots.Count == 0 || board.Slots[0] == null)
            return false;
        Transform container = board.Slots[0].transform.parent;
        GridLayoutGroup grid = container != null ? container.GetComponent<GridLayoutGroup>() : null;
        Image background = board.Background != null ? board.Background : FindBackground(board);
        if (grid == null || background == null)
            return false;
        RectTransform art = background.rectTransform;
        RectTransform gridRect = (RectTransform)grid.transform;
        Vector2 size = DrawnArtSize(board);
        if (size.x <= 0f || Mathf.Approximately(gridRect.lossyScale.x, 0f))
            return false;

        // Measure in the grid's own units: the art and the grid can sit under different scales.
        float side = size.x * art.lossyScale.x / gridRect.lossyScale.x;
        float cell = side * BoardTheme.SlotPitch;
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = Vector2.zero;
        grid.childAlignment = TextAnchor.MiddleCenter;
        if (gridRect.anchorMin == gridRect.anchorMax)
            gridRect.sizeDelta = new Vector2(cell * 4f, cell * 4f);
        // The holes are centred on the drawn art, so centre the grid there too.
        Vector3 centre = art.TransformPoint(art.rect.center);
        gridRect.position = new Vector3(centre.x, centre.y, gridRect.position.z);
        return true;
    }

    /// <summary>Square drawn by the background image (preserve aspect), in the image's local units.</summary>
    private static Vector2 DrawnArtSize(Board board)
    {
        Image background = board.Background != null ? board.Background : FindBackground(board);
        if (background == null)
            return Vector2.zero;
        Rect rect = background.rectTransform.rect;
        float side = Mathf.Min(rect.width, rect.height);
        return new Vector2(side, side);
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
