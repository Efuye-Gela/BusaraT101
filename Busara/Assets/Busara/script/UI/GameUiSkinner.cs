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
        Canvas canvas = GetComponentInParent<Canvas>();
        float canvasScale = canvas != null ? canvas.transform.lossyScale.x : 1f;
        foreach (var rule in Rules)
        {
            Transform root = transform.Find(rule.root);
            if (root == null)
                continue;
            foreach (Transform target in Named(root, rule.name, rule.parent))
                Skin(target, rule.role, canvasScale, changed);
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

    private void Skin(Transform target, Role role, float canvasScale, List<Object> changed)
    {
        if (role == Role.RawPanel)
        {
            SkinRawPanel(target, canvasScale, changed);
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
        image.color = new Color(1f, 1f, 1f, image.color.a);
        if (sprite.border.sqrMagnitude > 0f)
        {
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = FitsHeight(role)
                ? NaturalMultiplier(image.rectTransform, sprite)
                : Multiplier(image.rectTransform, sprite, BorderFor(role), canvasScale);
        }
        else
        {
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        if (role == Role.PowerButton)
            FitPowerLabel(target, changed);
    }

    // RawImage backgrounds cannot nine-slice, so draw the skin's panel in a child behind the content instead.
    private void SkinRawPanel(Transform target, float canvasScale, List<Object> changed)
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
        image.pixelsPerUnitMultiplier = Multiplier(image.rectTransform, skin.panel, skin.panelBorder, canvasScale);
        if (raw != null)
        {
            changed.Add(raw);
            // Keep the RawImage as the raycast target so the panel still blocks clicks; just stop drawing it.
            image.raycastTarget = false;
            raw.color = new Color(1f, 1f, 1f, 0f);
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
        text.color = power ? skin.accent : body ? skin.inkMuted : skin.ink;
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

    private static float NaturalMultiplier(RectTransform rect, Sprite sprite)
    {
        float height = rect.rect.height;
        return height > 1f ? Mathf.Max(0.01f, sprite.rect.height / height) : 1f;
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
    private static float Multiplier(RectTransform rect, Sprite sprite, float borderOnCanvas, float canvasScale)
    {
        float scale = Mathf.Abs(rect.lossyScale.x / Mathf.Max(0.0001f, canvasScale));
        float local = borderOnCanvas / Mathf.Max(0.0001f, scale);
        return Mathf.Max(0.01f, sprite.border.x / Mathf.Max(1f, local));
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
