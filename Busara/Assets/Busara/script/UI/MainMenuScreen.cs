using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays out the home screen: the Busara wordmark and tagline with the scene's Play and Quit buttons
/// on the left, and a showcase of board themes with pieces on the right. The scene's own buttons keep
/// their click events; this only moves and restyles them.
/// </summary>
public class MainMenuScreen : MonoBehaviour
{
    public BusaraUiStyle style;
    public ThemeCatalog catalog;
    [Tooltip("Seconds each board theme stays in the showcase.")]
    public float showcaseSeconds = 4.5f;

    // A checkerboard: no two pieces touch, as on a real Busara board, and tall pieces overlap the row behind.
    private static readonly (int row, int column, ResourceType type)[] ShowcaseLayout =
    {
        (0, 0, ResourceType.Fire), (0, 2, ResourceType.Water), (1, 1, ResourceType.Earth), (1, 3, ResourceType.Air),
        (2, 0, ResourceType.Air), (2, 2, ResourceType.Fire), (3, 1, ResourceType.Water), (3, 3, ResourceType.Earth),
    };
    private const float BoardSize = 640f;

    private UiKit ui;
    private Image board;
    private readonly List<Image> pieces = new List<Image>();
    private readonly List<int> pieceSlots = new List<int>(); // index into ShowcaseLayout for each piece
    private CanvasGroup showcaseGroup;
    private TMP_Text themeName;
    private TMP_Text themeDetail;

    private void Awake()
    {
        ui = new UiKit(style);
        BusaraUiStyle look = ui.Style;
        Transform canvas = transform;
        Transform legacy = canvas.Find("First entery");
        Button play = Find<Button>(legacy, "Play");
        Button quit = Find<Button>(legacy, "Quit");
        Transform title = legacy != null ? legacy.Find("Title") : null;
        if (title != null)
            title.gameObject.SetActive(false);

        Image wash = UiKit.Rect("Menu Wash", canvas).gameObject.AddComponent<Image>();
        wash.color = new Color(look.backdrop.r, look.backdrop.g, look.backdrop.b, 0.72f);
        wash.raycastTarget = false;
        UiKit.Stretch(wash.rectTransform);
        Transform background = canvas.Find("Background");
        wash.transform.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);

        RectTransform column = UiKit.Rect("Menu Column", canvas);
        column.anchorMin = new Vector2(0f, 0f);
        column.anchorMax = new Vector2(0f, 1f);
        column.pivot = new Vector2(0f, 0.5f);
        column.sizeDelta = new Vector2(700, -160);
        column.anchoredPosition = new Vector2(140, 0);
        VerticalLayoutGroup stack = UiKit.Vertical(column, 26, 0, TextAnchor.MiddleLeft);
        stack.childForceExpandWidth = false;
        if (look.logo != null)
        {
            Image logo = UiKit.Rect("Busara Logo", column).gameObject.AddComponent<Image>();
            logo.sprite = look.logo;
            logo.color = look.ink;
            logo.preserveAspect = true;
            logo.raycastTarget = false;
            UiKit.Size(logo, preferredWidth: 520, preferredHeight: 520 * look.logo.rect.height / Mathf.Max(1f, look.logo.rect.width));
        }
        else
            UiKit.Size(ui.Text("Wordmark", column, "BUSARA", look.Display, 140, look.ink), preferredWidth: 640, preferredHeight: 170);
        TMP_Text tagline = ui.Text("Tagline", column,
            "Build a kingdom from water, earth, fire and air. Gather virtues, use your power and outwit the other kingdoms.",
            look.Body, 28, look.inkMuted, TextAlignmentOptions.TopLeft);
        UiKit.Size(tagline, preferredWidth: 620, preferredHeight: 116);
        UiKit.Size(UiKit.Rect("Spacer", column), preferredHeight: 10);
        if (play != null)
            Restyle(play, column, "Play", UiButtonKind.Primary, new Vector2(420, 96), 36);
        if (quit != null)
        {
            Restyle(quit, column, "Quit", UiButtonKind.Ghost, new Vector2(420, 72), 28);
            // Browsers cannot close their own tab, so the web build has nothing to quit.
            quit.gameObject.SetActive(Application.platform != RuntimePlatform.WebGLPlayer);
        }
        if (legacy != null)
            legacy.SetAsLastSibling();
        column.SetAsLastSibling();

        BuildShowcase(canvas, look);
    }

    private void OnEnable()
    {
        if (board != null && catalog.themes.Count > 1)
            StartCoroutine(CycleThemes());
    }

    private void BuildShowcase(Transform canvas, BusaraUiStyle look)
    {
        if (catalog == null || catalog.themes.Count == 0)
            return;
        RectTransform showcase = UiKit.Rect("Theme Showcase", canvas);
        showcase.anchorMin = showcase.anchorMax = showcase.pivot = new Vector2(1f, 0.5f);
        showcase.sizeDelta = new Vector2(BoardSize, BoardSize + 110);
        showcase.anchoredPosition = new Vector2(-150, 0);
        showcaseGroup = showcase.gameObject.AddComponent<CanvasGroup>();
        showcaseGroup.blocksRaycasts = false;

        board = UiKit.Rect("Board", showcase).gameObject.AddComponent<Image>();
        board.raycastTarget = false;
        board.preserveAspect = true;
        UiKit.Pin(board.rectTransform, new Vector2(0.5f, 1f), new Vector2(BoardSize, BoardSize), Vector2.zero);
        // Lower rows are created last so their pieces draw over the row behind.
        var order = new List<int>();
        for (int i = 0; i < ShowcaseLayout.Length; i++)
            order.Add(i);
        order.Sort((a, b) => ShowcaseLayout[a].row.CompareTo(ShowcaseLayout[b].row));
        foreach (int index in order)
        {
            Image piece = UiKit.Rect($"Piece {index + 1}", board.transform).gameObject.AddComponent<Image>();
            piece.raycastTarget = false;
            piece.preserveAspect = true;
            pieces.Add(piece);
            pieceSlots.Add(index);
        }
        themeName = ui.Text("Theme Name", showcase, "", look.Heading, 32, look.ink, TextAlignmentOptions.Center);
        themeName.enableWordWrapping = false;
        UiKit.Pin(themeName.rectTransform, new Vector2(0.5f, 0f), new Vector2(BoardSize, 44), new Vector2(0, 46));
        themeDetail = ui.Text("Theme Detail", showcase, "", look.Body, 21, look.inkMuted, TextAlignmentOptions.Center);
        UiKit.Pin(themeDetail.rectTransform, new Vector2(0.5f, 0f), new Vector2(BoardSize, 30), new Vector2(0, 12));
        Show(0);
    }

    private IEnumerator CycleThemes()
    {
        int index = 0;
        var wait = new WaitForSeconds(Mathf.Max(1f, showcaseSeconds));
        while (true)
        {
            yield return wait;
            yield return Fade(1f, 0f);
            index = (index + 1) % catalog.themes.Count;
            Show(index);
            yield return Fade(0f, 1f);
        }
    }

    private IEnumerator Fade(float from, float to)
    {
        const float duration = 0.35f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            showcaseGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        showcaseGroup.alpha = to;
    }

    private void Show(int index)
    {
        BoardTheme theme = catalog.themes[index];
        if (theme == null)
            return;
        board.sprite = theme.board;
        int colour = catalog.playerColours.Count > 0 ? index % catalog.playerColours.Count : -1;
        float cell = BoardSize * BoardTheme.SlotPitch;
        float width = cell * ThemeManager.PieceScale;
        for (int i = 0; i < pieces.Count; i++)
        {
            Image piece = pieces[i];
            var (row, column, type) = ShowcaseLayout[pieceSlots[i]];
            Sprite sprite = PieceCompositor.Compose($"{theme.id}_{type}", theme.LayersFor(type), catalog.PieceColour(colour),
                ThemeCatalog.PlayerColourLayers);
            piece.sprite = sprite != null ? sprite : theme.PieceFor(type);
            piece.enabled = piece.sprite != null;
            RectTransform rect = piece.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, Mathf.Clamp01(theme.piecePivotY));
            rect.sizeDelta = new Vector2(width, width * Mathf.Max(0.1f, theme.pieceAspect));
            // Hole centres sit on a 208-unit pitch inside a 96-unit frame, in 1024-unit board art.
            rect.anchoredPosition = new Vector2(
                (96f + 208f * (column + 0.5f)) / 1024f * BoardSize - BoardSize / 2f,
                BoardSize / 2f - (96f + 208f * (row + 0.5f)) / 1024f * BoardSize);
        }
        themeName.text = theme.DisplayName;
        PlayerColour pieceColour = catalog.usePlayerColours ? catalog.ColourAt(colour) : null;
        string era = theme.EraLabel;
        themeDetail.text = pieceColour != null ? $"{era}  ·  {pieceColour.name} pieces" : era;
    }

    private void Restyle(Button button, Transform parent, string label, UiButtonKind kind, Vector2 size, float fontSize)
    {
        button.transform.SetParent(parent, false);
        Image image = button.targetGraphic as Image;
        BusaraUiStyle look = ui.Style;
        Color fill = kind == UiButtonKind.Primary ? look.accent : look.line;
        if (image != null)
        {
            UiKit.Round(image, 18, kind == UiButtonKind.Ghost);
            image.preserveAspect = false;
        }
        UiKit.Tint(button, fill);
        UiKit.Size(button, preferredWidth: size.x, preferredHeight: size.y);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
            text.font = look.Heading;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.color = kind == UiButtonKind.Primary ? look.accentInk : look.ink;
            text.alignment = TextAlignmentOptions.Center;
            UiKit.Stretch(text.rectTransform);
        }
    }

    private static T Find<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        return child != null ? child.GetComponent<T>() : null;
    }
}
