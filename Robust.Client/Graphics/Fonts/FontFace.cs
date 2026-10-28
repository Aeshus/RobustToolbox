using System;
using System.Collections.Generic;
using System.Text;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontFace(IClyde clyde, Library library, Face face, IFontMemoryHandle memoryHandle)
{
    private const float FreeType26Dot6Scale = 64f;
    private readonly Dictionary<(int, uint), FontInstance> _cachedInstances = new();
    private readonly Dictionary<Rune, uint> _glyphIndexCache = new();

    // Keep this alive to avoid it being GC'd.
    // ReSharper disable once UnusedMember.Local
    private readonly IFontMemoryHandle _memoryHandle = memoryHandle;

    private Face Face { get; } = face;

    internal uint FontDpi
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
    private uint GetGlyphIndex(Rune chr)
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

    internal void ClearSizeData()
    {
        foreach (var instance in _cachedInstances.Values)
        {
            instance.Dispose();
        }

        _cachedInstances.Clear();
    }

    internal bool TryGetGlyph(
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

        var radius = outlineThickness > 0 ? (int)MathF.Round(outlineThickness * scale * FreeType26Dot6Scale) : 0;

        GetInstance(size, scale).GetGlyph(glyph, radius, out metrics, out texture, out outlinedGlyph);
        return true;
    }

    internal CharMetrics? GetCharMetrics(int size, Rune codePoint, float scale)
    {
        return TryGetGlyph(size, codePoint, scale, 0, out var metrics, out _, out _)
            ? metrics
            : null;
    }

    internal FontInstance GetInstance(int size, float scale)
    {
        var verticalResolution = (uint)(scale * FontDpi);

        if (_cachedInstances.TryGetValue((size, verticalResolution), out var instance))
            return instance;

        instance = new FontInstance(clyde, library, Face, size, verticalResolution);
        _cachedInstances.Add((size, verticalResolution), instance);
        return instance;
    }
}
