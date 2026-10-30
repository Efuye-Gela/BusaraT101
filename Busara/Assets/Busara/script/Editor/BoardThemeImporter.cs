using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the sprite folders written by tools/theme-lab/export-sprites.mjs into BoardTheme assets
/// and keeps the ThemeCatalog in step. Re-run after exporting new or changed themes.
/// </summary>
public static class BoardThemeImporter
{
    public const string SpriteRoot = "Assets/Busara/Sprites/Themes";
    public const string LayerRoot = "Assets/Busara/Sprites/Pieces";
    public const string AssetRoot = "Assets/Busara/ScriptableObjects/Themes";
    public const string CatalogPath = AssetRoot + "/ThemeCatalog.asset";

#pragma warning disable 0649 // filled by JsonUtility
    [System.Serializable]
    private class Manifest
    {
        public string id;
        public string name;
        public string era;
        public string origin;
        public Colors colors;
        public Geometry geometry;
        public Layers layers;
    }

    [System.Serializable]
    private class Layers
    {
        public LayerEntry[] water;
        public LayerEntry[] earth;
        public LayerEntry[] fire;
        public LayerEntry[] air;
        public LayerEntry[] hidden;
    }

    [System.Serializable]
    private class LayerEntry
    {
        public string name;
        public string file; // relative to Assets/Busara/Sprites
        public string tint; // null for untinted layers
    }

    // shapes.json from tools/theme-lab/export-shapes.mjs: the theme redrawn for square and hexagon pieces.
    [System.Serializable]
    private class ShapeFile
    {
        public ShapeSet shapes;
    }

    [System.Serializable]
    private class ShapeSet
    {
        public ShapeEntry square;
        public ShapeEntry hex;
    }

    [System.Serializable]
    private class ShapeEntry
    {
        public float pieceAspect = 1f;
        public float piecePivotY = 0.5f;
        public Layers layers;
    }

    [System.Serializable]
    private class Geometry
    {
        public float pieceAspect = 1f;
        public float piecePivotY = 0.5f;
    }

    [System.Serializable]
    private class Colors
    {
        public string accent;
        public string danger;
        public string light;
    }
#pragma warning restore 0649

    [MenuItem("Tools/Busara/Board Themes/Import Themes From Sprites")]
    public static void ImportAll()
    {
        if (!AssetDatabase.IsValidFolder(AssetRoot))
            AssetDatabase.CreateFolder("Assets/Busara/ScriptableObjects", "Themes");
        var imported = new List<BoardTheme>();
        // Piece layers are composited on the CPU at runtime, so they keep a readable copy.
        if (AssetDatabase.IsValidFolder(LayerRoot))
            ConfigureSprites(LayerRoot, true);
        foreach (string folder in AssetDatabase.GetSubFolders(SpriteRoot).OrderBy(path => path))
        {
            string manifestPath = folder + "/manifest.json";
            if (!File.Exists(manifestPath))
                continue;
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            ConfigureSprites(folder);
            imported.Add(CreateOrUpdate(folder, manifest));
        }

        ThemeCatalog catalog = AssetDatabase.LoadAssetAtPath<ThemeCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ThemeCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        // Classic first, then the rest in a stable order; keep any seat defaults already chosen.
        catalog.themes = imported.OrderBy(theme => theme.id == "classic" ? 0 : 1).ThenBy(theme => theme.name).ToList();
        catalog.seatDefaults.RemoveAll(theme => theme == null);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"Imported {imported.Count} board themes into {CatalogPath}.");
    }

    [MenuItem("Tools/Busara/Board Themes/Apply Seat Themes In Open Scene")]
    public static void ApplySeatThemesInScene()
    {
        ThemeManager manager = Object.FindFirstObjectByType<ThemeManager>();
        if (manager == null)
        {
            Debug.LogWarning("Add a ThemeManager to the scene first (GameScene has one under Managers).");
            return;
        }
        // Record every object Apply touches so the change can be undone and is saved with the scene.
        var touched = new List<Object>();
        foreach (Board board in Object.FindObjectsByType<Board>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            touched.AddRange(board.GetComponentsInChildren<UnityEngine.UI.Graphic>(true));
            touched.AddRange(board.GetComponentsInChildren<RectTransform>(true));
            touched.AddRange(board.GetComponentsInChildren<UnityEngine.UI.GridLayoutGroup>(true));
        }
        Undo.RecordObjects(touched.ToArray(), "Apply seat board themes");
        manager.ApplySeatDefaults(true);
        foreach (Object item in touched)
            EditorUtility.SetDirty(item);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    private static void ConfigureSprites(string folder, bool readable = false)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                continue;
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single || importer.mipmapEnabled || !importer.alphaIsTransparency ||
                importer.isReadable != readable;
            if (!changed)
                continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }
    }

    private static BoardTheme CreateOrUpdate(string folder, Manifest manifest)
    {
        string assetPath = $"{AssetRoot}/{manifest.name}.asset";
        BoardTheme theme = AssetDatabase.LoadAssetAtPath<BoardTheme>(assetPath);
        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<BoardTheme>();
            AssetDatabase.CreateAsset(theme, assetPath);
        }
        theme.id = manifest.id;
        theme.displayName = manifest.name;
        theme.era = manifest.era;
        theme.origin = manifest.origin;
        Sprite Load(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}/{file}");
        theme.board = Load("board.png");
        theme.turnGlow = Load("board_turn_glow.png");
        theme.water = Load("piece_water.png");
        theme.earth = Load("piece_earth.png");
        theme.fire = Load("piece_fire.png");
        theme.air = Load("piece_air.png");
        theme.hidden = Load("piece_hidden.png");
        theme.pieceSelected = Load("piece_state_selected.png");
        theme.pieceThreatened = Load("piece_state_threatened.png");
        theme.pieceShielded = Load("piece_state_shielded.png");
        theme.slotHover = Load("slot_state_hover.png");
        theme.slotLegal = Load("slot_state_legal.png");
        theme.slotBlocked = Load("slot_state_blocked.png");
        theme.slotSelected = Load("slot_state_selected.png");
        theme.slotTarget = Load("slot_state_target.png");
        if (manifest.geometry != null && manifest.geometry.pieceAspect > 0f)
        {
            theme.pieceAspect = manifest.geometry.pieceAspect;
            theme.piecePivotY = manifest.geometry.piecePivotY;
        }
        Layers layers = manifest.layers ?? new Layers();
        theme.waterLayers = LoadLayers(layers.water);
        theme.earthLayers = LoadLayers(layers.earth);
        theme.fireLayers = LoadLayers(layers.fire);
        theme.airLayers = LoadLayers(layers.air);
        theme.hiddenLayers = LoadLayers(layers.hidden);
        theme.shapes = new List<ShapeVariant>();
        TextAsset shapesFile = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + "/shapes.json");
        ShapeFile shapeFile = shapesFile != null ? JsonUtility.FromJson<ShapeFile>(shapesFile.text) : null;
        if (shapeFile?.shapes != null)
            foreach (var (shape, entry) in new[] { ("square", shapeFile.shapes.square), ("hex", shapeFile.shapes.hex) })
            {
                if (entry?.layers == null)
                    continue;
                theme.shapes.Add(new ShapeVariant
                {
                    shape = shape,
                    board = Load($"board_{shape}.png"),
                    pieceAspect = entry.pieceAspect,
                    piecePivotY = entry.piecePivotY,
                    waterLayers = LoadLayers(entry.layers.water),
                    earthLayers = LoadLayers(entry.layers.earth),
                    fireLayers = LoadLayers(entry.layers.fire),
                    airLayers = LoadLayers(entry.layers.air),
                    hiddenLayers = LoadLayers(entry.layers.hidden),
                    slotHover = Load($"slot_state_hover_{shape}.png"),
                    slotLegal = Load($"slot_state_legal_{shape}.png"),
                    slotBlocked = Load($"slot_state_blocked_{shape}.png"),
                    slotSelected = Load($"slot_state_selected_{shape}.png"),
                    slotTarget = Load($"slot_state_target_{shape}.png"),
                });
            }
        if (manifest.colors != null)
        {
            if (ColorUtility.TryParseHtmlString(manifest.colors.accent, out Color accent)) theme.accent = accent;
            if (ColorUtility.TryParseHtmlString(manifest.colors.danger, out Color danger)) theme.danger = danger;
            if (ColorUtility.TryParseHtmlString(manifest.colors.light, out Color light)) theme.light = light;
        }
        EditorUtility.SetDirty(theme);
        return theme;
    }

    private static List<PieceLayer> LoadLayers(LayerEntry[] entries)
    {
        var layers = new List<PieceLayer>();
        if (entries == null)
            return layers;
        foreach (LayerEntry entry in entries)
        {
            bool tinted = ColorUtility.TryParseHtmlString(entry.tint, out Color tint);
            layers.Add(new PieceLayer
            {
                name = entry.name,
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Busara/Sprites/" + entry.file),
                tinted = tinted,
                tint = tinted ? tint : Color.white,
            });
        }
        return layers;
    }
}
