using System.Collections.Generic;
using System.Text;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontFaceHandle(IClyde clyde, Library library, Face face, IFontMemoryHandle memoryHandle)
{
    // Keep this alive to avoid it being GC'd.
    // ReSharper disable once NotAccessedField.Local
    private readonly IFontMemoryHandle _memoryHandle = memoryHandle;
    private readonly Dictionary<(int, float), FontInstance> _cachedInstances = new();
    private readonly Dictionary<Rune, uint> _glyphIndexCache = new();

    internal IClyde Clyde { get; } = clyde;
    internal Library Library { get; } = library;
    internal Face Face { get; } = face;

    // 0 is a sentinel value for failure
    public uint GetGlyphIndex(Rune chr)
    {
        if (_glyphIndexCache.TryGetValue(chr, out var glyph))
        {
            return glyph;
        }

        // Check FreeType to see if it exists.
        var index = Face.GetCharIndex((uint)chr.Value);

        _glyphIndexCache.Add(chr, index);

        return index;
    }

    public void ClearSizeData()
    {
        foreach (var instance in _cachedInstances.Values)
        {
            foreach (var ownedTexture in instance.AtlasTextures)
            {
                ownedTexture.Dispose();
            }

            instance.Stroker.Dispose();
        }

        _cachedInstances.Clear();
    }

    public uint FontDpi
    {
        get;
        set
        {
            if (field == value)
                return;

            ClearSizeData();
            field = value;
        }
    }

    public bool TryGetGlyph(
        int size,
        Rune codePoint,
        float scale,
        float outlineThickness,
        out CharMetrics metrics,
        out Texture? texture,
        out OutlinedGlyph? outlinedGlyph)
    {
        metrics = default;
        texture = null;
        outlinedGlyph = null;

        var glyph = GetGlyphIndex(codePoint);

        if (glyph == 0)
            return false;

        var instance = GetInstance(size, scale);
        var glyphInfo = instance.EnsureGlyphCached(scale, glyph);
        metrics = glyphInfo.Metrics;
        texture = glyphInfo.Texture;

        // Don't render the outline character unless necessary
        if (texture == null || outlineThickness <= 0)
            return true;

        outlinedGlyph = instance.GetOutlinedChar(glyph, scale, outlineThickness);
        return true;
    }

    public CharMetrics? GetCharMetrics(int size, Rune codePoint, float scale)
    {
        return TryGetGlyph(size, codePoint, scale, 0, out var metrics, out _, out _)
            ? metrics
            : null;
    }

    public int GetAscent(int size, float scale)
    {
        return GetInstance(size, scale).Ascent;
    }

    public int GetDescent(int size, float scale)
    {
        return GetInstance(size, scale).Descent;
    }

    public int GetHeight(int size, float scale)
    {
        return GetInstance(size, scale).Height;
    }

    public int GetLineHeight(int size, float scale)
    {
        return GetInstance(size, scale).LineHeight;
    }

    private FontInstance GetInstance(int size, float scale)
    {
        if (_cachedInstances.TryGetValue((size, scale), out var instance))
            return instance;

        instance = new FontInstance(this, size, scale);
        _cachedInstances.Add((size, scale), instance);
        return instance;
    }
}

