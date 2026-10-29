using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Restyles GameScene's side panels, popups and action buttons with a <see cref="UiSkin"/>.
/// Objects are found by name under the canvas, so the scene keeps its layout and every button keeps
/// its click events; only sprites, fonts and text colours change. Applying twice is harmless, and
/// <i>Tools > Busara > UI Skin > Apply Skin In Open Scene</i> runs the same code in the editor.
/// </summary>
public class GameUiSkinner : MonoBehaviour
{
    public UiSkin skin;

    private enum Role { Panel, RawPanel, Inset, Header, Card, Tray, Popup, ActionButton, PowerButton, Holder, PrimaryButton, SecondaryButton, Hide }

    // (top-level object under the canvas, object name, role, required parent name or null)
    private static readonly (string root, string name, Role role, string parent)[] Rules =
    {
        ("Right SidePanel", "Right SidePanel", Role.Panel, null),
        ("Right SidePanel", "UI Buttons", Role.Hide, null),
        ("Right SidePanel", "NameArea", Role.Header, null),
        ("Right SidePanel", "Trade", Role.ActionButton, "Action Buttons"),
        ("Right SidePanel", "weapon", Role.ActionButton, "Action Buttons"),
        ("Right SidePanel", "Draw", Role.ActionButton, "Action Buttons"),
        ("Right SidePanel", "forge", Role.ActionButton, "Action Buttons"),
        ("Right SidePanel", "Use Power", Role.PowerButton, "Action Buttons"),
        ("Right SidePanel", "Resource holder image", Role.Holder, null),
        ("Right SidePanel", "Background", Role.Tray, "layer"),

        ("Left Side panel", "BackgroundProfile", Role.RawPanel, null),
        ("Left Side panel", "BackgroundVirtue", Role.RawPanel, null),
        ("Left Side panel", "BackgroundHint", Role.RawPanel, null),
        ("Left Side panel", "Resource combo", Role.Inset, null),
        ("Left Side panel", "Profile Button", Role.Header, null),
        ("Left Side panel", "Virute Button", Role.Header, null),
        ("Left Side panel", "Kingdome Name", Role.Header, null),
        ("Left Side panel", "PowerName", Role.Header, null),
        ("Left Side panel", "Title win", Role.Header, null),
        ("Left Side panel", "Kingdom discription", Role.Inset, null),
        ("Left Side panel", "Power Discription", Role.Inset, null),
        ("Left Side panel", "Virtue Goals", Role.Inset, null),
        ("Left Side panel", "Virtue", Role.Card, "Virtue Goals"),
        ("Left Side panel", "Virtue (1)", Role.Card, "Virtue Goals"),
        ("Left Side panel", "Virtue (2)", Role.Card, "Virtue Goals"),
        ("Left Side panel", "Virtue collection", Role.Inset, "Virtue collection"),
        ("Left Side panel", "More", Role.SecondaryButton, null),
        ("Left Side panel", "Error Massages", Role.Popup, null),

        ("Popup", "BG", Role.Popup, null),
        ("Popup", "CloseBtn", Role.PrimaryButton, null),
    };

    private static readonly string[] TextRoots = { "Right SidePanel", "Left Side panel", "Popup" };
    private const string SkinBackgroundName = "Skin Background";

    // Canvas reference pixels per unit. Nine-slice borders are measured in sprite pixels and divided by
    // spritePPU / referencePPU, so a canvas at 1000 (as GameScene's is) draws them 10x larger than at 100.
    private float referencePixelsPerUnit = 100f;

    private void Awake()
    {
        Apply();
    }

    /// <summary>Applies <see cref="skin"/> to the canvas this component sits on. Returns the objects it changed.</summary>
    public List<Object> Apply()
    {
        var changed = new List<Object>();
        if (skin == null)
            return changed;
        Transform canvas = transform;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        Canvas rootCanvas = GetComponent<Canvas>();
        referencePixelsPerUnit = scaler != null ? scaler.referencePixelsPerUnit
            : rootCanvas != null ? rootCanvas.referencePixelsPerUnit : 100f;
        foreach (var rule in Rules)
        {
            Transform root = transform.Find(rule.root);
            if (root == null)
                continue;
            foreach (Transform target in Named(root, rule.name, rule.parent))
                Skin(target, rule.role, canvas, changed);
        }
        foreach (string rootName in TextRoots)
        {
            Transform root = transform.Find(rootName);
            if (root == null)
                continue;
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
                SkinText(text, changed);
        }
        return changed;
    }

    private void Skin(Transform target, Role role, Transform canvas, List<Object> changed)
    {
        if (role == Role.RawPanel)
        {
            SkinRawPanel(target, canvas, changed);
            return;
        }
        Image image = target.GetComponent<Image>();
        if (image == null)
            return;
        changed.Add(image);
        if (role == Role.Hide)
        {
            image.color = new Color(1f, 1f, 1f, 0f);
            return;
        }
        Sprite sprite = SpriteFor(role);
        if (sprite == null)
            return;
        image.sprite = sprite;
        // Several of these (the tabs, More) are invisible hit areas in the scene; the skin gives them art.
        image.color = Color.white;
        if (sprite.border.sqrMagnitude > 0f)
        {
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = FitsHeight(role)
                ? NaturalMultiplier(image.rectTransform, sprite)
                : Multiplier(image.rectTransform, sprite, BorderFor(role), canvas);
        }
        else
        {
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        if (role == Role.PowerButton)
            FitPowerLabel(target, changed);
        if (target.name == "Error Massages")
            FitMessageBanner(image, canvas, changed);
    }

    // RawImage backgrounds cannot nine-slice, so draw the skin's panel in a child behind the content instead.
    private void SkinRawPanel(Transform target, Transform canvas, List<Object> changed)
    {
        RawImage raw = target.GetComponent<RawImage>();
        Transform existing = target.Find(SkinBackgroundName);
        Image image = existing != null ? existing.GetComponent<Image>() : null;
        if (image == null)
        {
            var child = new GameObject(SkinBackgroundName, typeof(RectTransform), typeof(Image));
            child.transform.SetParent(target, false);
            child.transform.SetAsFirstSibling();
            image = child.GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            changed.Add(child);
        }
        changed.Add(image);
        image.sprite = skin.panel;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = Multiplier(image.rectTransform, skin.panel, skin.panelBorder, canvas);
        if (raw != null)
        {
            changed.Add(raw);
            // Keep the RawImage as the raycast target so the panel still blocks clicks; just stop drawing it.
            image.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
        }
    }

    // The side bar message was a 400-unit square whose text grew to fill it. A short banner covers less
    // of the panel; its text is capped at a readable size and kept inside the neon frame.
    private void FitMessageBanner(Image panel, Transform canvas, List<Object> changed)
    {
        const float width = 390f, height = 176f, frame = 26f;
        RectTransform rect = panel.rectTransform;
        changed.Add(rect);
        rect.sizeDelta = new Vector2(width, height);
        panel.pixelsPerUnitMultiplier = Multiplier(rect, panel.sprite, frame, canvas);
        TMP_Text message = panel.GetComponentInChildren<TMP_Text>(true);
        if (message != null && message.name == "Info Side")
        {
            changed.Add(message);
            changed.Add(message.rectTransform);
            RectTransform area = message.rectTransform;
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(frame + 22f, frame + 14f);
            area.offsetMax = new Vector2(-(frame + 22f), -(frame + 14f));
            message.margin = Vector4.zero;
            message.enableAutoSizing = true;
            message.fontSizeMin = 14f;
            message.fontSizeMax = 28f;
            message.enableWordWrapping = true;
            message.alignment = TextAlignmentOptions.Center;
        }
        Transform close = panel.transform.Find("CloseBtn");
        if (close is RectTransform button)
        {
            changed.Add(button);
            button.anchorMin = button.anchorMax = Vector2.one;
            button.pivot = new Vector2(0.5f, 0.5f);
            button.sizeDelta = new Vector2(44f, 44f);
            button.anchoredPosition = new Vector2(-frame + 4f, -frame + 4f);
        }
    }

    // The knob's dark centre holds the label.
    private void FitPowerLabel(Transform button, List<Object> changed)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null)
            return;
        changed.Add(label.rectTransform);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.27f, 0.27f);
        rect.anchorMax = new Vector2(0.73f, 0.73f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        label.enableAutoSizing = true;
        label.fontSizeMin = 8f;
        label.fontSizeMax = 64f;
        label.alignment = TextAlignmentOptions.Center;
    }

    private void SkinText(TMP_Text text, List<Object> changed)
    {
        changed.Add(text);
        bool body = text.name == "Info" || text.name == "Info Side" || text.name == "Massage" ||
            (text.text != null && text.text.Length > 40);
        TMP_FontAsset font = body ? skin.bodyFont : skin.headingFont;
        if (font != null && text.font != font)
            text.font = font;
        bool power = text.transform.parent != null && text.transform.parent.name == "Use Power";
        bool message = text.name == "Info Side" || text.name == "Massage";
        text.color = power ? skin.accent : message ? skin.ink : body ? skin.inkMuted : skin.ink;
        if (body || power)
            return;
        // The skin's heading font runs wider than the scene's: shrink a label to fit rather than let it
        // overflow, and never break a single word ("Kingdom") across lines.
        if (!text.enableAutoSizing)
        {
            text.fontSizeMax = text.fontSize;
            text.fontSizeMin = Mathf.Max(8f, text.fontSize * 0.45f);
            text.enableAutoSizing = true;
        }
        if (text.text != null && text.text.Trim().IndexOf(' ') < 0)
            text.enableWordWrapping = false;
    }

    private Sprite SpriteFor(Role role)
    {
        switch (role)
        {
            case Role.Panel: return skin.panel;
            case Role.Inset: return skin.inset;
            case Role.Header: return skin.header;
            case Role.Card: return skin.card;
            case Role.Tray: return skin.tray;
            case Role.Popup: return skin.popup;
            case Role.ActionButton: return skin.actionButton;
            case Role.PowerButton: return skin.powerButton;
            case Role.Holder: return skin.holder;
            case Role.PrimaryButton: return skin.primaryButton;
            case Role.SecondaryButton: return skin.secondaryButton;
            default: return null;
        }
    }

    // Tabs, cards and buttons read best at the sprite's own proportions: scale the art to the rect's height.
    private static bool FitsHeight(Role role)
    {
        return role == Role.Header || role == Role.Card || role == Role.PrimaryButton || role == Role.SecondaryButton;
    }

    private float NaturalMultiplier(RectTransform rect, Sprite sprite)
    {
        float height = rect.rect.height;
        return height > 1f ? Mathf.Max(0.01f, sprite.rect.height / height * PixelScale(sprite)) : PixelScale(sprite);
    }

    private float PixelScale(Sprite sprite)
    {
        return referencePixelsPerUnit / Mathf.Max(0.01f, sprite.pixelsPerUnit);
    }

    private float BorderFor(Role role)
    {
        switch (role)
        {
            case Role.Panel:
            case Role.Tray: return skin.panelBorder;
            case Role.Popup: return skin.popupBorder;
            default: return skin.insetBorder;
        }
    }

    // Nested panels sit under different scales, so size each border for how big it ends up on the canvas.
    private float Multiplier(RectTransform rect, Sprite sprite, float borderOnCanvas, Transform canvas)
    {
        float local = borderOnCanvas / Mathf.Max(0.0001f, ScaleUnder(rect, canvas));
        return Mathf.Max(0.01f, sprite.border.x / Mathf.Max(1f, local) * PixelScale(sprite));
    }

    // Scale of rect's units in canvas units, from the local scales in between. The canvas's own scale is
    // left out on purpose: GameScene saves it as 0 and the CanvasScaler only sets it after Awake.
    private static float ScaleUnder(Transform rect, Transform canvas)
    {
        float scale = 1f;
        for (Transform t = rect; t != null && t != canvas; t = t.parent)
            scale *= Mathf.Abs(t.localScale.x);
        return scale;
    }

    private static IEnumerable<Transform> Named(Transform root, string objectName, string parentName)
    {
        foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
        {
            if (candidate.name.Trim() != objectName)
                continue;
            if (parentName != null && (candidate.parent == null || candidate.parent.name.Trim() != parentName))
                continue;
            yield return candidate;
        }
    }
}
