using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Home screen slideshow: cycles the authored showcase board and pieces through every board theme in
/// the catalog, using each theme's exported sprites. The images and layout are scene objects; this only
/// changes which sprites they show and fades between themes.
/// </summary>
public class ThemeShowcase : MonoBehaviour
{
    public ThemeCatalog catalog;
    public UnityEngine.UI.Image board;
    [Tooltip("Piece images in ShowcaseLayout order.")]
    public List<UnityEngine.UI.Image> pieces = new List<UnityEngine.UI.Image>();
    public TMP_Text themeName;
    public TMP_Text themeDetail;
    public CanvasGroup fade;
    [Tooltip("Seconds each board theme stays on screen.")]
    public float seconds = 4.5f;

    /// <summary>Element shown in each piece image: a checkerboard, so no two pieces touch.</summary>
    public static readonly ResourceType[] ShowcaseLayout =
    {
        ResourceType.Fire, ResourceType.Water, ResourceType.Earth, ResourceType.Air,
        ResourceType.Air, ResourceType.Fire, ResourceType.Water, ResourceType.Earth,
    };

    private void OnEnable()
    {
        if (catalog == null || catalog.themes.Count == 0 || board == null)
            return;
        Show(0);
        if (catalog.themes.Count > 1)
            StartCoroutine(Cycle());
    }

    private IEnumerator Cycle()
    {
        int index = 0;
        var wait = new WaitForSeconds(Mathf.Max(1f, seconds));
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
        if (fade == null)
            yield break;
        const float duration = 0.35f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            fade.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        fade.alpha = to;
    }

    private void Show(int index)
    {
        BoardTheme theme = catalog.themes[index];
        if (theme == null)
            return;
        board.sprite = theme.board;
        float width = board.rectTransform.rect.width * BoardTheme.SlotPitch * ThemeManager.PieceScale;
        for (int i = 0; i < pieces.Count && i < ShowcaseLayout.Length; i++)
        {
            UnityEngine.UI.Image piece = pieces[i];
            piece.sprite = theme.PieceFor(ShowcaseLayout[i]);
            piece.enabled = piece.sprite != null;
            // Tall shapes rise from their base: keep the base on the hole and let the sprite grow upward.
            RectTransform rect = piece.rectTransform;
            rect.pivot = new Vector2(0.5f, Mathf.Clamp01(theme.piecePivotY));
            rect.sizeDelta = new Vector2(width, width * Mathf.Max(0.1f, theme.pieceAspect));
        }
        if (themeName != null)
            themeName.text = theme.DisplayName;
        if (themeDetail != null)
            themeDetail.text = theme.EraLabel;
    }
}
