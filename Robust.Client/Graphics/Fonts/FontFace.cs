using System;
using System.Collections.Generic;
using System.Text;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontFace(IClyde clyde, Library library, Face face, IFontMemoryHandle memoryHandle)
{
    private const float FreeType26Dot6Scale = 64f;
    private readonly Dictionary<(int, float), FontInstance> _cachedInstances = new();
    private readonly Dictionary<Rune, uint> _glyphIndexCache = new();

    // Keep this alive to avoid it being GC'd.
    // ReSharper disable once NotAccessedField.Local
    private readonly IFontMemoryHandle _memoryHandle = memoryHandle;

    internal IClyde Clyde { get; } = clyde;
    internal Library Library { get; } = library;
    internal Face Face { get; } = face;

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
            instance.Dispose();
        }

        _cachedInstances.Clear();
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
        var glyphInfo = instance.RenderGlyph(glyph);
        metrics = glyphInfo.Metrics;
        texture = glyphInfo.Texture;

        // Don't render the outline character unless necessary
        if (texture == null || outlineThickness <= 0)
            return true;

        outlinedGlyph =
            instance.RenderGlyphWithOutline(glyph, (int)MathF.Round(outlineThickness * scale * FreeType26Dot6Scale));
        return true;
    }

    public CharMetrics? GetCharMetrics(int size, Rune codePoint, float scale)
    {
        return TryGetGlyph(size, codePoint, scale, 0, out var metrics, out _, out _)
            ? metrics
            : null;
    }

    public FontInstance GetInstance(int size, float scale)
    {
        if (_cachedInstances.TryGetValue((size, scale), out var instance))
            return instance;

        instance = new FontInstance(Clyde, Library, Face, size, (uint)(scale * FontDpi));
        _cachedInstances.Add((size, scale), instance);
        return instance;
    }
}
