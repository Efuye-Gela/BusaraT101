using TMPro;
using UnityEngine;

/// <summary>
/// A look for the in-game side panels, popups and action buttons, drawn from a theme's
/// interface kit (tools/theme-lab exports one per theme under Themes/&lt;id&gt;/ui).
/// <see cref="GameUiSkinner"/> applies it to the scene.
/// </summary>
[CreateAssetMenu(menuName = "Busara/UI Skin", fileName = "UiSkin")]
public class UiSkin : ScriptableObject
{
    public string displayName;

    [Header("Panels (nine-slice)")]
    [Tooltip("Side panel backgrounds.")]
    public Sprite panel;
    [Tooltip("Recessed areas inside a panel: descriptions, goal lists, virtue collection.")]
    public Sprite inset;
    [Tooltip("Small headers and tabs: kingdom name, power name, Kingdom / Virtue tabs.")]
    public Sprite header;
    [Tooltip("One entry in a list, such as a virtue goal.")]
    public Sprite card;
    [Tooltip("Secondary surface, such as the setup resources tray.")]
    public Sprite tray;
    [Tooltip("Popups and error messages.")]
    public Sprite popup;

    [Header("Buttons")]
    [Tooltip("Round action buttons: Forge, Draw, Trade, Weapon.")]
    public Sprite actionButton;
    [Tooltip("The Use Power button.")]
    public Sprite powerButton;
    [Tooltip("The drawn-resource holder.")]
    public Sprite holder;
    [Tooltip("Main call to action in popups (nine-slice).")]
    public Sprite primaryButton;
    [Tooltip("Low-emphasis buttons such as More (nine-slice).")]
    public Sprite secondaryButton;

    [Header("Menus")]
    [Tooltip("Decorative strip repeated across the home screen.")]
    public Sprite frieze;
    [Tooltip("Bezel around the home screen's board showcase (nine-slice).")]
    public Sprite frame;

    [Header("Nine-slice border on screen, in canvas units")]
    [Tooltip("Tabs, cards and buttons are drawn at their own proportions, scaled to their height; these set the rest.")]
    public float panelBorder = 26f;
    public float insetBorder = 14f;
    public float popupBorder = 40f;

    [Header("Text")]
    public TMP_FontAsset headingFont;
    public TMP_FontAsset bodyFont;
    public Color ink = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
    public Color inkMuted = new Color32(0xC9, 0xCF, 0xD8, 0xFF);
    [Tooltip("Text on the power button and other highlights.")]
    public Color accent = new Color32(0xF5, 0x9A, 0x2E, 0xFF);
}
