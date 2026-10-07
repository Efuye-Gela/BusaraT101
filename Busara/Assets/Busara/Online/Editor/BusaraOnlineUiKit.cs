using System.IO;
using System.Linq;
using Busara.Online.Client;
using Eg.UI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;

// Authored assets for the online client: baked shapes, button/label prefabs and top-left layout helpers.
public static class BusaraOnlineUiKit
{
    public const string Root = "Assets/Busara/Online/";
    public const string RoundPath = Root + "Art/OnlineRound.png";
    public const string CirclePath = Root + "Art/OnlineCircle.png";
    public const string ButtonPath = Root + "Prefabs/OnlineButton.prefab";
    public const string LabelPath = Root + "Prefabs/OnlineLabel.prefab";
    public const float BakedRadius = 40f;

    public static TMP_FontAsset Font(string weight) =>
        Require(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Busara/Font/" + weight + "_SDF.asset"), weight);

    public static Sprite Sprite(string path) =>
        Require(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(), path);

    // Shapes are baked once so their GUIDs stay stable across scene rebuilds.
    public static Sprite RoundSprite()
    {
        if (!File.Exists(RoundPath)) RoundedSprite.Bake(RoundPath, BakedRadius);
        return Sprite(RoundPath);
    }

    public static Sprite CircleSprite()
    {
        if (!File.Exists(CirclePath))
        {
            var raster = new SdfRaster(128, 128);
            raster.Fill(SdfShapes.Circle(new Vector2(64, 64), 62), Color.white);
            SpriteBaker.WritePng(CirclePath, raster, SpriteImportOptions.Default);
        }
        return Sprite(CirclePath);
    }

    public static Button ButtonPrefab(Sprite round, TMP_FontAsset font)
    {
        var go = new GameObject("OnlineButton", typeof(RectTransform), typeof(Image), typeof(Button));
        ((RectTransform)go.transform).sizeDelta = new Vector2(320, 56);
        var plate = Rounded(go.GetComponent<Image>(), round, OnlineTheme.SurfaceRaised);
        plate.raycastTarget = true;
        var button = go.GetComponent<Button>();
        button.targetGraphic = plate;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = colors.selectedColor = new Color(1.18f, 1.15f, 1.08f);
        colors.pressedColor = new Color(.82f, .82f, .82f);
        colors.disabledColor = new Color(.55f, .55f, .55f, .55f);
        colors.fadeDuration = .08f;
        button.colors = colors;
        var label = Text(go.transform, "Label", "Action", font, OnlineTheme.ButtonSize, OnlineTheme.Text);
        Stretch(label.rectTransform, 14, 6);
        label.alignment = TextAlignmentOptions.Center;
        return SavePrefab(go, ButtonPath).GetComponent<Button>();
    }

    public static TMP_Text LabelPrefab(TMP_FontAsset font)
    {
        var go = new GameObject("OnlineLabel", typeof(RectTransform));
        var label = Text(go.transform, null, "", font, OnlineTheme.BodySize, OnlineTheme.Text);
        return SavePrefab(label.gameObject, LabelPath).GetComponent<TMP_Text>();
    }

    public static RectTransform Node(string name, Transform parent, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    // A raised surface with a thin amber edge, echoing the authored panel frames.
    public static Image Panel(string name, Transform parent, float x, float y, float width, float height, Sprite round, Color fill)
    {
        Rounded(Node(name + " edge", parent, x - 2, y - 2, width + 4, height + 4).gameObject.AddComponent<Image>(),
            round, OnlineTheme.Hex(0xFFB21A, .55f));
        return Rounded(Node(name, parent, x, y, width, height).gameObject.AddComponent<Image>(), round, fill);
    }

    public static Image Rounded(Image image, Sprite round, Color color)
    {
        image.sprite = round;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = BakedRadius / OnlineTheme.Radius;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TMP_Text Text(Transform parent, string name, string value, TMP_FontAsset font, float size, Color color)
    {
        GameObject host = name == null ? parent.gameObject : Node(name, parent, 0, 0, 400, 40).gameObject;
        var text = host.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.richText = false;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    public static void Stretch(RectTransform rect, float padX = 0, float padY = 0)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(padX, padY);
        rect.offsetMax = new Vector2(-padX, -padY);
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var asset = PrefabUtility.SaveAsPrefabAsset(go, path, out bool saved);
        Object.DestroyImmediate(go);
        if (!saved) throw new BuildFailedException("Could not save " + path);
        return asset;
    }

    private static T Require<T>(T value, string what) where T : Object =>
        value != null ? value : throw new BuildFailedException("Online presentation is missing " + what + ".");
}
