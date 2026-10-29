using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Button looks used by the menus built in code.</summary>
public enum UiButtonKind
{
    /// <summary>Gold, for the one action that moves the player forward.</summary>
    Primary,
    /// <summary>Raised neutral surface.</summary>
    Secondary,
    /// <summary>Outline only, for low-emphasis actions such as going back.</summary>
    Ghost,
    /// <summary>Round, for single-glyph controls.</summary>
    Icon,
}

/// <summary>
/// Small builder for uGUI screens created in code: rounded panels, text and buttons in the
/// <see cref="BusaraUiStyle"/>. Rounded shapes are generated once at runtime, so the screens
/// need no extra sprite assets.
/// </summary>
public class UiKit
{
    public readonly BusaraUiStyle Style;

    public UiKit(BusaraUiStyle style)
    {
        Style = BusaraUiStyle.Resolve(style);
    }

    public static RectTransform Rect(string objectName, Transform parent)
    {
        var child = new GameObject(objectName, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Places <paramref name="rect"/> at a fixed size around an anchor point of its parent.</summary>
    public static void Pin(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    /// <summary>Adds or updates a LayoutElement; negative values leave a setting unset.</summary>
    public static LayoutElement Size(Component target, float minWidth = -1, float preferredWidth = -1, float flexibleWidth = -1,
        float minHeight = -1, float preferredHeight = -1, float flexibleHeight = -1)
    {
        LayoutElement size = target.GetComponent<LayoutElement>();
        if (size == null)
            size = target.gameObject.AddComponent<LayoutElement>();
        size.minWidth = minWidth;
        size.preferredWidth = preferredWidth;
        size.flexibleWidth = flexibleWidth;
        size.minHeight = minHeight;
        size.preferredHeight = preferredHeight;
        size.flexibleHeight = flexibleHeight;
        return size;
    }

    public static VerticalLayoutGroup Vertical(Component target, float spacing, int padding = 0,
        TextAnchor alignment = TextAnchor.UpperLeft)
    {
        VerticalLayoutGroup layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static HorizontalLayoutGroup Horizontal(Component target, float spacing, int padding = 0,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        HorizontalLayoutGroup layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return layout;
    }

    /// <summary>A rounded rectangle (or outline) filled with <paramref name="colour"/>.</summary>
    public Image Panel(string objectName, Transform parent, Color colour, float radius = 18f, bool outline = false)
    {
        Image image = Rect(objectName, parent).gameObject.AddComponent<Image>();
        Round(image, radius, outline);
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    public static void Round(Image image, float radius, bool outline = false)
    {
        image.sprite = outline ? UiShapes.Outline : UiShapes.Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = UiShapes.Radius / Mathf.Max(1f, radius);
    }

    public TMP_Text Text(string objectName, Transform parent, string value, TMP_FontAsset font, float fontSize, Color colour,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        TextMeshProUGUI text = Rect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : Style.Body;
        text.fontSize = fontSize;
        text.color = colour;
        text.text = value;
        text.richText = false;
        text.raycastTarget = false;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    /// <summary>A small caps label above a field, like "KINGDOM".</summary>
    public TMP_Text Caption(string objectName, Transform parent, string value)
    {
        TMP_Text text = Text(objectName, parent, value.ToUpperInvariant(), Style.Heading, 17, Style.inkMuted);
        text.characterSpacing = 8;
        Size(text, preferredHeight: 24);
        return text;
    }

    public Button Button(string objectName, Transform parent, string label, UiButtonKind kind, float fontSize = 28)
    {
        RectTransform rect = Rect(objectName, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Color fill, ink;
        switch (kind)
        {
            case UiButtonKind.Primary: fill = Style.accent; ink = Style.accentInk; break;
            case UiButtonKind.Ghost: fill = Style.line; ink = Style.ink; break;
            default: fill = Style.raised; ink = Style.ink; break;
        }
        if (kind == UiButtonKind.Icon)
        {
            image.sprite = UiShapes.Circle;
            image.preserveAspect = true;
        }
        else
            Round(image, 16, kind == UiButtonKind.Ghost);
        Tint(button, fill);
        TMP_Text text = Text("Label", rect, label, kind == UiButtonKind.Icon ? Style.Body : Style.Heading, fontSize, ink,
            TextAlignmentOptions.Center);
        text.enableWordWrapping = false;
        Stretch(text.rectTransform);
        return button;
    }

    /// <summary>Colours a button's graphic through its colour block, so hover lifts and press sinks the base colour.</summary>
    public static void Tint(UnityEngine.UI.Selectable selectable, Color fill)
    {
        selectable.targetGraphic.color = Color.white;
        ColorBlock colours = selectable.colors;
        colours.normalColor = fill;
        colours.highlightedColor = Color.Lerp(fill, Color.white, 0.14f);
        colours.selectedColor = Color.Lerp(fill, Color.white, 0.08f);
        colours.pressedColor = Color.Lerp(fill, Color.black, 0.18f);
        colours.disabledColor = new Color(fill.r, fill.g, fill.b, fill.a * 0.35f);
        colours.colorMultiplier = 1f;
        colours.fadeDuration = 0.08f;
        selectable.colors = colours;
    }
}

/// <summary>Anti-aliased rounded shapes, generated once and shared by every screen.</summary>
public static class UiShapes
{
    /// <summary>Corner radius of the generated nine-slice sprites, in pixels.</summary>
    public const float Radius = 32f;
    private const float OutlineWidth = 3f;

    private static Sprite rounded, outline, circle, ring;

    public static Sprite Rounded => rounded != null ? rounded : rounded = Make("Rounded", 2 * (int)Radius + 2, Radius, 0f, true);
    public static Sprite Outline => outline != null ? outline : outline = Make("Rounded Outline", 2 * (int)Radius + 2, Radius, OutlineWidth, true);
    public static Sprite Circle => circle != null ? circle : circle = Make("Circle", 128, 64f, 0f, false);
    public static Sprite Ring => ring != null ? ring : ring = Make("Ring", 128, 64f, 8f, false);

    private static Sprite Make(string spriteName, int size, float radius, float stroke, bool sliced)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var pixels = new Color32[size * size];
        float half = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square filling the texture; negative inside.
                float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
                float distance = outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                float alpha = Mathf.Clamp01(0.5f - distance);
                if (stroke > 0f)
                    alpha *= Mathf.Clamp01(distance + stroke + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Vector4 border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
