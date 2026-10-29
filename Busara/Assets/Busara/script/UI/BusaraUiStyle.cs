using TMPro;
using UnityEngine;

/// <summary>
/// Fonts, colours and art shared by the screens built in code (main menu, player setup).
/// Every field has a usable default, so a screen still renders when no asset is assigned.
/// </summary>
[CreateAssetMenu(menuName = "Busara/UI Style", fileName = "BusaraUiStyle")]
public class BusaraUiStyle : ScriptableObject
{
    [Header("Fonts")]
    public TMP_FontAsset displayFont;
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;

    [Header("Art")]
    [Tooltip("Light version of the Busara wordmark, tinted by the screens.")]
    public Sprite logo;
    [Tooltip("Full-screen backdrop behind menus.")]
    public Sprite background;

    [Header("Colours")]
    public Color backdrop = new Color32(0x0D, 0x11, 0x16, 0xFF);
    public Color surface = new Color32(0x17, 0x1D, 0x25, 0xF2);
    public Color raised = new Color32(0x22, 0x2A, 0x35, 0xFF);
    public Color line = new Color32(0x34, 0x3E, 0x4B, 0xFF);
    public Color ink = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
    public Color inkMuted = new Color32(0xA5, 0xAE, 0xBA, 0xFF);
    public Color accent = new Color32(0xE2, 0xB5, 0x4A, 0xFF);
    public Color accentInk = new Color32(0x1C, 0x15, 0x07, 0xFF);
    public Color danger = new Color32(0xE8, 0x7A, 0x5D, 0xFF);

    public TMP_FontAsset Display => displayFont != null ? displayFont : Heading;
    public TMP_FontAsset Heading => headingFont != null ? headingFont : Body;
    public TMP_FontAsset Body => bodyFont != null ? bodyFont : TMP_Settings.defaultFontAsset;

    private static BusaraUiStyle fallback;

    /// <summary><paramref name="style"/>, or a shared default when it is missing.</summary>
    public static BusaraUiStyle Resolve(BusaraUiStyle style)
    {
        if (style != null)
            return style;
        if (fallback == null)
        {
            fallback = CreateInstance<BusaraUiStyle>();
            fallback.hideFlags = HideFlags.HideAndDontSave;
        }
        return fallback;
    }
}
