using UnityEngine;

/// <summary>
/// The local player's board and piece look, chosen on the main menu's Customize screen and saved between
/// sessions. The setup screen gives it to Player 1. Piece shape "" keeps the theme's own pieces and round
/// holes; "square" and "hex" use square or hexagon pieces in matching holes.
/// </summary>
public static class PlayerCustomization
{
    public static readonly string[] Shapes = { "", "square", "hex" };
    private const string ThemeKey = "busara.custom.theme", ShapeKey = "busara.custom.shape", ColourKey = "busara.custom.colour";

    public static bool Saved => PlayerPrefs.HasKey(ThemeKey);
    public static string ThemeId => PlayerPrefs.GetString(ThemeKey, "");
    public static string PieceShape => PlayerPrefs.GetString(ShapeKey, "");
    public static int ColourIndex => PlayerPrefs.GetInt(ColourKey, 0);

    public static void Save(string themeId, string shape, int colourIndex)
    {
        PlayerPrefs.SetString(ThemeKey, themeId ?? "");
        PlayerPrefs.SetString(ShapeKey, shape ?? "");
        PlayerPrefs.SetInt(ColourKey, colourIndex);
        PlayerPrefs.Save();
    }

    public static string ShapeName(string shape) =>
        shape == "square" ? "Square" : shape == "hex" ? "Hexagon" : "Theme";
}
