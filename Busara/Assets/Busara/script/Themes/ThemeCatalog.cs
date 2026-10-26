using System.Collections.Generic;
using UnityEngine;

/// <summary>Every board theme players can choose from, plus the theme each seat starts with.</summary>
[CreateAssetMenu(menuName = "Busara/Theme Catalog", fileName = "ThemeCatalog")]
public class ThemeCatalog : ScriptableObject
{
    public List<BoardTheme> themes = new List<BoardTheme>();
    [Tooltip("Theme for seat 1, 2, 3, 4 until the player picks another one.")]
    public List<BoardTheme> seatDefaults = new List<BoardTheme>();

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
