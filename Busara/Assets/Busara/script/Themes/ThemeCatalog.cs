using System.Collections.Generic;
using UnityEngine;

/// <summary>Every board theme players can choose from, plus the theme each seat starts with.</summary>
[CreateAssetMenu(menuName = "Busara/Theme Catalog", fileName = "ThemeCatalog")]
public class ThemeCatalog : ScriptableObject
{
    public List<BoardTheme> themes = new List<BoardTheme>();
    [Tooltip("Theme for seat 1, 2, 3, 4 until the player picks another one.")]
    public List<BoardTheme> seatDefaults = new List<BoardTheme>();

    [Header("Player colours")]
    [Tooltip("Recolour each seat's piece trim and motif layers so players on the same theme stay distinct.")]
    public bool usePlayerColours = true;
    [Tooltip("Colour for seat 1, 2, 3, 4.")]
    public List<Color> seatColours = new List<Color>
    {
        new Color32(0xE2, 0xB5, 0x4A, 0xFF), // gold
        new Color32(0xD9, 0xDD, 0xE3, 0xFF), // silver
        new Color32(0xC8, 0x75, 0x4A, 0xFF), // copper
        new Color32(0x4F, 0xB5, 0x8A, 0xFF), // jade
    };

    /// <summary>Piece layers that take the seat colour; the paint and lighting keep the theme's look.</summary>
    public static readonly string[] PlayerColourLayers = { "trim", "motif" };

    public BoardTheme Find(string id)
    {
        foreach (BoardTheme theme in themes)
            if (theme != null && theme.id == id)
                return theme;
        return null;
    }

    public BoardTheme SeatDefault(int seat)
    {
        if (seat >= 0 && seat < seatDefaults.Count && seatDefaults[seat] != null)
            return seatDefaults[seat];
        foreach (BoardTheme theme in themes)
            if (theme != null)
                return theme;
        return null;
    }

    /// <summary>The seat's colour, or null when player colours are off or the seat has none.</summary>
    public Color? SeatColour(int seat)
    {
        if (!usePlayerColours || seat < 0 || seat >= seatColours.Count)
            return null;
        return seatColours[seat];
    }

    /// <summary>The theme <paramref name="step"/> places after <paramref name="current"/>, wrapping around.</summary>
    public BoardTheme Next(BoardTheme current, int step)
    {
        if (themes.Count == 0)
            return null;
        int index = Mathf.Max(0, themes.IndexOf(current));
        for (int i = 0; i < themes.Count; i++)
        {
            index = ((index + step) % themes.Count + themes.Count) % themes.Count;
            if (themes[index] != null)
                return themes[index];
        }
        return current;
    }
}
