using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds piece sprites from <see cref="PieceLayer"/> stacks. Layers are blended on the CPU in sRGB,
/// exactly as tools/theme-lab renders and verifies them, so a composite matches the pre-composited
/// piece sprite whatever the project's colour space (stacked UI Images would blend in linear light
/// and wash the lighting out). Results are cached per stack and tint set.
/// </summary>
public static class PieceCompositor
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    private static readonly HashSet<string> warned = new HashSet<string>();

    /// <summary>Composite of <paramref name="layers"/>, with <paramref name="overrideTint"/> replacing the tint of the
    /// layers named in <paramref name="overrideLayers"/>. Null when a layer is missing or its texture is not readable.</summary>
    public static Sprite Compose(string key, IList<PieceLayer> layers, Color? overrideTint, ICollection<string> overrideLayers)
    {
        if (layers == null || layers.Count == 0)
            return null;
        var tints = new Color[layers.Count];
        var cacheKey = new System.Text.StringBuilder(key);
        for (int i = 0; i < layers.Count; i++)
        {
            PieceLayer layer = layers[i];
            if (layer == null || layer.sprite == null)
                return null;
            tints[i] = !layer.tinted ? Color.white
                : overrideTint.HasValue && overrideLayers != null && overrideLayers.Contains(layer.name) ? overrideTint.Value
                : layer.tint;
            cacheKey.Append('|').Append(layer.sprite.GetInstanceID()).Append(':').Append(ColorUtility.ToHtmlStringRGB(tints[i]));
        }
        string id = cacheKey.ToString();
        if (cache.TryGetValue(id, out Sprite cached) && cached != null)
            return cached;

        Texture2D first = layers[0].sprite.texture;
        int width = first.width, height = first.height;
        var accumulated = new Color[width * height]; // premultiplied
        for (int i = 0; i < layers.Count; i++)
        {
            Texture2D texture = layers[i].sprite.texture;
            if (!texture.isReadable || texture.width != width || texture.height != height)
            {
                if (warned.Add(texture.name))
                    Debug.LogWarning($"Piece layer '{texture.name}' needs Read/Write enabled and a {width}x{height} size; using the pre-composited piece.");
                return null;
            }
            Color32[] pixels = texture.GetPixels32();
            Color tint = tints[i];
            for (int p = 0; p < pixels.Length; p++)
            {
                Color32 src = pixels[p];
                if (src.a == 0)
                    continue;
                float a = src.a / 255f, keep = 1f - a;
                Color dst = accumulated[p];
                dst.r = src.r / 255f * tint.r * a + dst.r * keep;
                dst.g = src.g / 255f * tint.g * a + dst.g * keep;
                dst.b = src.b / 255f * tint.b * a + dst.b * keep;
                dst.a = a + dst.a * keep;
                accumulated[p] = dst;
            }
        }
        var output = new Color32[accumulated.Length];
        for (int p = 0; p < accumulated.Length; p++)
        {
            Color c = accumulated[p];
            output[p] = c.a > 0f ? new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a) : new Color(0f, 0f, 0f, 0f);
        }
        var composite = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = key,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        composite.SetPixels32(output);
        composite.Apply(false, true);
        Sprite sprite = Sprite.Create(composite, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        sprite.name = key;
        cache[id] = sprite;
        return sprite;
    }
}
