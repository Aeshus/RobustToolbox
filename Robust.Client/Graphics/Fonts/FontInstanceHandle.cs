using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

[PublicAPI]
internal sealed class FontInstanceHandle(IClyde clyde, Library library, FontFaceHandle faceHandle, int size)
{
    private readonly IClyde _clyde = clyde;
    private readonly Library _library = library;

    private const int SheetWidth = 256;
    private const int SheetHeight = 256;

    private int Size { get; } = size;
    private readonly Dictionary<float, FontInstance> _instances = new();

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

    public void ClearSizeData()
    {
        foreach (var instance in _instances.Values)
        {
            foreach (var ownedTexture in instance.AtlasTextures)
            {
                ownedTexture.Dispose();
            }

            instance.Stroker.Dispose();
        }

        _instances.Clear();
    }

    public bool TryGetGlyph(
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

        var glyph = faceHandle.GetGlyphIndex(codePoint);

        if (glyph == 0)
            return false;

        var instance = GetInstance(scale);
        var glyphInfo = instance.EnsureGlyphCached(scale, glyph);
        metrics = glyphInfo.Metrics;
        texture = glyphInfo.Texture;
        outlinedGlyph = null;

        // Don't render the outline character unless necessary
        if (texture == null || outlineThickness <= 0)
            return true;

        outlinedGlyph = instance.GetOutlinedChar(glyph, scale, outlineThickness);
        return true;
    }

    public Texture? GetCharTexture(Rune codePoint, float scale)
    {
        return TryGetGlyph(codePoint, scale, 0, out _, out var texture, out _) ? texture : null;
    }

    public OutlinedGlyph? GetOutlinedChar(Rune codePoint, float scale, float thickness)
    {
        if (thickness <= 0)
            return null;

        var glyph = faceHandle.GetGlyphIndex(codePoint);
        if (glyph == 0)
            return null;

        return GetInstance(scale).GetOutlinedChar(glyph, scale, thickness);
    }

    public CharMetrics? GetCharMetrics(Rune codePoint, float scale)
    {
        return TryGetGlyph(codePoint, scale, 0, out var metrics, out _, out _)
            ? metrics
            : null;
    }

    public int GetAscent(float scale)
    {
        return GetInstance(scale).Ascent;
    }

    public int GetDescent(float scale)
    {
        return GetInstance(scale).Descent;
    }

    public int GetHeight(float scale)
    {
        return GetInstance(scale).Height;
    }

    public int GetLineHeight(float scale)
    {
        return GetInstance(scale).LineHeight;
    }

    private FontInstance GetInstance(float scale)
    {
        return _instances.TryGetValue(scale, out var instance)
            ? instance
            : CreateFontInstance(this, scale);
    }

    private FontInstance CreateFontInstance(FontInstanceHandle instanceHandle, float scale)
    {
        var instance = new FontInstance(instanceHandle, scale);
        _instances.Add(scale, instance);
        return instance;
    }
}
