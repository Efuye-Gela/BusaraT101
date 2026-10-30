using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// References into one authored player card on the setup screen. <see cref="PlayerSetupUI"/> fills it
/// for a seat; the card's layout and look live in the scene.
/// </summary>
public class PlayerSetupCard : MonoBehaviour
{
    [Header("Seat")]
    public UnityEngine.UI.Image colourBar;
    public UnityEngine.UI.Image colourDot;
    public TMP_Text seatLabel;
    public UnityEngine.UI.Button removeButton;
    public TMP_InputField nameInput;
    [Tooltip("Switches the seat between a human player and a bot.")]
    public UnityEngine.UI.Button controlButton;
    public TMP_Text controlLabel;

    [Header("Board theme and piece colour")]
    public UnityEngine.UI.Image boardPreview;
    public UnityEngine.UI.Image piecePreview;
    public UnityEngine.UI.Button pieceButton;
    public UnityEngine.UI.Button previousTheme;
    public UnityEngine.UI.Button nextTheme;
    public TMP_Text themeName;
    public TMP_Text themeDetail;
    [Tooltip("One per ThemeCatalog.playerColours entry, in order.")]
    public List<UnityEngine.UI.Button> swatches = new List<UnityEngine.UI.Button>();
    [Tooltip("The selection ring on each swatch, in the same order.")]
    public List<UnityEngine.UI.Image> swatchRings = new List<UnityEngine.UI.Image>();

    [Header("Kingdom")]
    public TMP_Dropdown kingdomDropdown;
    public TMP_Text powerName;
    public TMP_Text powerDescription;
    public GameObject winCondition;
    public List<UnityEngine.UI.Image> goalIcons = new List<UnityEngine.UI.Image>();
    public List<TMP_Text> goalCounts = new List<TMP_Text>();
}
