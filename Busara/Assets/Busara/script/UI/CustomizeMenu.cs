using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The main menu's Customize screen: the player's board theme, piece shape (the theme's own, square or hexagon,
/// with holes to match) and piece colour, previewed live and saved for the next game. Every control is authored;
/// option buttons show an authored inactive "Selected" fill, as on the New Game screen.
/// </summary>
public class CustomizeMenu : MonoBehaviour
{
    public ThemeCatalog catalog;
    public GameObject mainColumn;
    public GameObject customizeColumn;
    [Tooltip("The home screen's theme slideshow, hidden while the preview shows the player's own look.")]
    public GameObject showcase;
    public GameObject preview;
    public UnityEngine.UI.Image boardPreview;
    [Tooltip("Water, Earth, Fire and Air, in order.")]
    public List<UnityEngine.UI.Image> piecePreviews = new List<UnityEngine.UI.Image>();
    public TMP_Text themeName;
    public TMP_Text themeDetail;
    public UnityEngine.UI.Button previousTheme;
    public UnityEngine.UI.Button nextTheme;
    [Tooltip("Theme, Square and Hexagon, in order (PlayerCustomization.Shapes).")]
    public List<UnityEngine.UI.Button> shapeButtons = new List<UnityEngine.UI.Button>();
    [Tooltip("One per ThemeCatalog.playerColours entry, in order.")]
    public List<UnityEngine.UI.Button> swatches = new List<UnityEngine.UI.Button>();
    public TMP_Text note;
    public UnityEngine.UI.Button saveButton;
    public UnityEngine.UI.Button backButton;
    public Color labelColour = new Color32(0xF4, 0xEF, 0xE6, 0xFF);
    public Color selectedLabelColour = new Color32(0x2A, 0x16, 0x06, 0xFF);

    private BoardTheme theme;
    private string shape = "";
    private int colour;
    private bool wired;

    public void Open()
    {
        Wire();
        theme = catalog != null ? catalog.Find(PlayerCustomization.ThemeId) ?? catalog.SeatDefault(0) : null;
        shape = PlayerCustomization.PieceShape;
        colour = PlayerCustomization.Saved ? PlayerCustomization.ColourIndex : 0;
        mainColumn.SetActive(false);
        customizeColumn.SetActive(true);
        if (showcase != null) showcase.SetActive(false);
        if (preview != null) preview.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        customizeColumn.SetActive(false);
        mainColumn.SetActive(true);
        if (preview != null) preview.SetActive(false);
        if (showcase != null) showcase.SetActive(true);
    }

    private void Wire()
    {
        if (wired)
            return;
        wired = true;
        previousTheme.onClick.AddListener(() => StepTheme(-1));
        nextTheme.onClick.AddListener(() => StepTheme(1));
        for (int i = 0; i < shapeButtons.Count && i < PlayerCustomization.Shapes.Length; i++)
        {
            string chosen = PlayerCustomization.Shapes[i];
            shapeButtons[i].onClick.AddListener(() => { shape = chosen; Refresh(); });
        }
        for (int i = 0; i < swatches.Count; i++)
        {
            int index = i;
            swatches[i].onClick.AddListener(() => { colour = index; Refresh(); });
        }
        saveButton.onClick.AddListener(() =>
        {
            PlayerCustomization.Save(theme != null ? theme.id : "", shape, colour);
            Close();
        });
        backButton.onClick.AddListener(Close);
    }

    private void StepTheme(int step)
    {
        if (catalog == null)
            return;
        theme = catalog.Next(theme, step);
        Refresh();
    }

    private void Refresh()
    {
        if (theme == null)
            return;
        // A theme drawn only one way (its own pieces) cannot take square or hexagon pieces.
        bool shapes = theme.Variant("square") != null;
        string shown = shapes ? shape : "";
        boardPreview.sprite = theme.BoardFor(shown);
        boardPreview.preserveAspect = true;
        for (int i = 0; i < piecePreviews.Count; i++)
        {
            piecePreviews[i].sprite = ThemeManager.ComposePiece(catalog, theme, (ResourceType)i, colour, shown);
            piecePreviews[i].preserveAspect = true;
        }
        themeName.text = theme.DisplayName;
        themeDetail.text = theme.EraLabel;
        for (int i = 0; i < shapeButtons.Count && i < PlayerCustomization.Shapes.Length; i++)
        {
            shapeButtons[i].interactable = i == 0 || shapes;
            Show(shapeButtons[i], PlayerCustomization.Shapes[i] == shown);
        }
        for (int i = 0; i < swatches.Count; i++)
        {
            bool exists = catalog != null && i < catalog.playerColours.Count;
            swatches[i].gameObject.SetActive(exists);
            if (!exists)
                continue;
            swatches[i].targetGraphic.color = catalog.playerColours[i].colour;
            Transform ring = swatches[i].transform.Find("Selected");
            if (ring != null)
                ring.gameObject.SetActive(i == colour);
        }
        note.text = shapes
            ? PlayerCustomization.ShapeName(shown) + " pieces" + (shown.Length > 0 ? " sit in " + PlayerCustomization.ShapeName(shown).ToLowerInvariant() + " holes." : " in the theme's round holes.")
            : "This theme keeps its own pieces and round holes.";
    }

    private void Show(UnityEngine.UI.Button button, bool selected)
    {
        Transform fill = button.transform.Find("Selected");
        if (fill != null)
            fill.gameObject.SetActive(selected);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.color = selected ? selectedLabelColour : labelColour;
    }
}
