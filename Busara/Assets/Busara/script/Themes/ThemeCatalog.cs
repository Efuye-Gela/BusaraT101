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
    [Tooltip("Recolour each player's piece trim and motif layers so players on the same theme stay distinct.")]
    public bool usePlayerColours = true;
    [Tooltip("Colours players choose from in setup. Seat 1, 2, 3, 4 start on the first four.")]
    public List<PlayerColour> playerColours = new List<PlayerColour>
    {
        new PlayerColour("Gold", new Color32(0xE2, 0xB5, 0x4A, 0xFF)),
        new PlayerColour("Silver", new Color32(0xD9, 0xDD, 0xE3, 0xFF)),
        new PlayerColour("Copper", new Color32(0xC8, 0x75, 0x4A, 0xFF)),
        new PlayerColour("Jade", new Color32(0x4F, 0xB5, 0x8A, 0xFF)),
        new PlayerColour("Ruby", new Color32(0xC8, 0x3A, 0x3A, 0xFF)),
        new PlayerColour("Lapis", new Color32(0x3E, 0x6F, 0xD8, 0xFF)),
        new PlayerColour("Amethyst", new Color32(0x9B, 0x6B, 0xD6, 0xFF)),
        new PlayerColour("Ivory", new Color32(0xF2, 0xE8, 0xD0, 0xFF)),
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

    /// <summary>The colour at <paramref name="index"/> (wrapping), or null when there is none.</summary>
    public PlayerColour ColourAt(int index)
    {
        if (index < 0 || playerColours.Count == 0)
            return null;
        return playerColours[index % playerColours.Count];
    }

    /// <summary>The tint for colour <paramref name="index"/>, or null when player colours are off.</summary>
    public Color? PieceColour(int index)
    {
        PlayerColour colour = usePlayerColours ? ColourAt(index) : null;
        return colour != null ? colour.colour : (Color?)null;
    }

    /// <summary>The colour index <paramref name="step"/> places after <paramref name="current"/>, wrapping around.</summary>
    public int NextColour(int current, int step)
    {
        int count = playerColours.Count;
        return count == 0 ? -1 : ((Mathf.Max(0, current) + step) % count + count) % count;
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

[System.Serializable]
public class PlayerColour
{
    public string name;
    public Color colour = Color.white;

    public PlayerColour(string name, Color colour)
    {
        this.name = name;
        this.colour = colour;
    }
}
