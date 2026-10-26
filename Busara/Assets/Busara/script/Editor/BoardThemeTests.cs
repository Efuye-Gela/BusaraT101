using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class BoardThemeTests
{
    private static ThemeCatalog LoadCatalog()
    {
        ThemeCatalog catalog = AssetDatabase.LoadAssetAtPath<ThemeCatalog>(BoardThemeImporter.CatalogPath);
        Assert.That(catalog, Is.Not.Null, "Run Tools > Busara > Board Themes > Import Themes From Sprites.");
        return catalog;
    }

    [Test]
    public void CatalogListsEveryExportedThemeOnce()
    {
        ThemeCatalog catalog = LoadCatalog();
        string[] folders = AssetDatabase.GetSubFolders(BoardThemeImporter.SpriteRoot)
            .Where(folder => System.IO.File.Exists(folder + "/manifest.json")).ToArray();
        Assert.That(catalog.themes, Has.Count.EqualTo(folders.Length));
        Assert.That(catalog.themes, Has.None.Null);
        Assert.That(catalog.themes.Select(theme => theme.id), Is.Unique);
        Assert.That(catalog.Find("classic"), Is.Not.Null);
    }

    [Test]
    public void EveryThemeHasAllSprites()
    {
        foreach (BoardTheme theme in LoadCatalog().themes)
        {
            var sprites = new Dictionary<string, Sprite>
            {
                ["board"] = theme.board, ["turnGlow"] = theme.turnGlow,
                ["water"] = theme.water, ["earth"] = theme.earth, ["fire"] = theme.fire, ["air"] = theme.air,
                ["hidden"] = theme.hidden, ["pieceSelected"] = theme.pieceSelected,
                ["pieceThreatened"] = theme.pieceThreatened, ["pieceShielded"] = theme.pieceShielded,
                ["slotHover"] = theme.slotHover, ["slotLegal"] = theme.slotLegal, ["slotBlocked"] = theme.slotBlocked,
                ["slotSelected"] = theme.slotSelected, ["slotTarget"] = theme.slotTarget,
            };
            foreach (var pair in sprites)
                Assert.That(pair.Value, Is.Not.Null, $"{theme.id} is missing {pair.Key}");
            Assert.That(theme.PieceFor(ResourceType.Water), Is.SameAs(theme.water));
            Assert.That(theme.PieceFor(ResourceType.Earth), Is.SameAs(theme.earth));
            Assert.That(theme.PieceFor(ResourceType.Fire), Is.SameAs(theme.fire));
            Assert.That(theme.PieceFor(ResourceType.Air), Is.SameAs(theme.air));
        }
    }

    [Test]
    public void SeatDefaultsAndCyclingStayInsideTheCatalog()
    {
        ThemeCatalog catalog = LoadCatalog();
        for (int seat = 0; seat < 4; seat++)
            Assert.That(catalog.themes, Does.Contain(catalog.SeatDefault(seat)));
        BoardTheme first = catalog.themes[0];
        Assert.That(catalog.Next(first, -1), Is.SameAs(catalog.themes[catalog.themes.Count - 1]));
        Assert.That(catalog.Next(first, catalog.themes.Count), Is.SameAs(first));
    }

    [Test]
    public void ApplyLaysSlotsOnTheBoardSpriteGrid()
    {
        BoardTheme theme = LoadCatalog().themes[0];
        var root = new GameObject("Boards", typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup));
        root.GetComponent<UnityEngine.UI.GridLayoutGroup>().cellSize = new Vector2(1400, 1400);
        var boardObject = new GameObject("Board", typeof(RectTransform));
        boardObject.transform.SetParent(root.transform, false);
        Board board = boardObject.AddComponent<Board>();
        var background = new GameObject("Board Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        background.transform.SetParent(boardObject.transform, false);
        var slots = new GameObject("Board Slots", typeof(RectTransform), typeof(UnityEngine.UI.GridLayoutGroup));
        slots.transform.SetParent(boardObject.transform, false);
        var slotObject = new GameObject("slot 0", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        slotObject.transform.SetParent(slots.transform, false);
        Slot slot = slotObject.AddComponent<Slot>();
        slot.board = board;
        board.Slots = new List<Slot> { slot };
        var manager = new GameObject("Themes").AddComponent<ThemeManager>();
        try
        {
            manager.Apply(board, theme);
            Assert.That(background.GetComponent<UnityEngine.UI.Image>().sprite, Is.SameAs(theme.board));
            Assert.That(slots.GetComponent<UnityEngine.UI.GridLayoutGroup>().cellSize.x,
                Is.EqualTo(1400f * BoardTheme.SlotPitch).Within(0.01f));
            Assert.That(slotObject.GetComponent<UnityEngine.UI.Image>().color.a, Is.EqualTo(0f));
            Assert.That(manager.ThemeOf(board), Is.SameAs(theme));
        }
        finally
        {
            Object.DestroyImmediate(manager.gameObject);
            Object.DestroyImmediate(root);
        }
    }
}
