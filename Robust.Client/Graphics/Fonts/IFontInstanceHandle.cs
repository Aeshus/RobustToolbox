using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

/// <summary>
///     Metrics for a single glyph in a font.
///     Refer to https://www.freetype.org/freetype2/docs/glyphs/glyphs-3.html for more information.
/// </summary>
public readonly struct CharMetrics
{
    /// <summary>
    ///     The horizontal distance between the origin and the left of the drawn glyph.
    /// </summary>
    public readonly int BearingX;

    /// <summary>
    ///     The vertical distance between the origin and the top of the drawn glyph.
    /// </summary>
    public readonly int BearingY;

    /// <summary>
    ///     How much to advance the origin after drawing the glyph.
    /// </summary>
    public readonly int Advance;

    /// <summary>
    ///     The width of the drawn glyph.
    /// </summary>
    public readonly int Width;

    /// <summary>
    ///     The height of the drawn glyph.
    /// </summary>
    public readonly int Height;

    public CharMetrics(int bearingX, int bearingY, int advance, int width, int height)
    {
        BearingX = bearingX;
        BearingY = bearingY;
        Advance = advance;
        Width = width;
        Height = height;
    }
}

internal readonly record struct OutlinedGlyph(Texture Texture, int Left, int Top);

[PublicAPI]
internal sealed class FontInstanceHandle
{
    public FontFaceHandle FaceHandle { get; }
    public int Size { get; }
    private readonly Dictionary<float, ScaledFontData> _scaledData = new();
    private readonly FontManager _fontManager;
    public readonly Dictionary<Rune, uint> GlyphMap;

    public FontInstanceHandle(FontManager fontManager, int size, FontFaceHandle faceHandle)
    {
        GlyphMap = new Dictionary<Rune, uint>();
        _fontManager = fontManager;
        Size = size;
        FaceHandle = faceHandle;
    }

    public void ClearSizeData()
    {
        foreach (var scaleData in _scaledData)
        {
            foreach (var ownedTexture in scaleData.Value.AtlasTextures)
            {
                ownedTexture.Dispose();
            }

            scaleData.Value.Stroker.Dispose();
        }

        _scaledData.Clear();
    }

    public bool TryGetGlyph(
        Rune codePoint,
        float scale,
        float outlineThickness,
        out CharMetrics metrics,
        out Texture? texture,
        out OutlinedGlyph? outlinedGlyph)
    {
        var glyph = GetGlyph(codePoint);
        if (glyph == 0)
        {
            metrics = default;
            texture = null;
            outlinedGlyph = null;
            return false;
        }

        var scaled = GetScaleDatum(scale);
        var glyphInfo = _fontManager.EnsureGlyphCached(this, scaled, scale, glyph);
        metrics = glyphInfo.Metrics;
        texture = glyphInfo.Texture;
        outlinedGlyph = null;

        // An outline is never rendered without the normal glyph, so avoid generating or
        // looking up outline data when the glyph has no bitmap.
        if (texture == null || outlineThickness <= 0)
            return true;

        var radius = (int)MathF.Round(outlineThickness * scale * FontManager.FreeType26Dot6Scale);
        if (radius <= 0)
            return true;

        var outlinedInfo = _fontManager.EnsureOutlinedGlyphCached(this, scaled, scale, glyph, radius);
        if (outlinedInfo.Texture != null)
            outlinedGlyph = new OutlinedGlyph(outlinedInfo.Texture, outlinedInfo.Left, outlinedInfo.Top);

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

        var glyph = GetGlyph(codePoint);
        if (glyph == 0)
            return null;

        var radius = (int)MathF.Round(thickness * scale * FontManager.FreeType26Dot6Scale);
        if (radius <= 0)
            return null;

        var scaled = GetScaleDatum(scale);
        var info = _fontManager.EnsureOutlinedGlyphCached(this, scaled, scale, glyph, radius);
        if (info.Texture == null)
            return null;

        return new OutlinedGlyph(info.Texture, info.Left, info.Top);
    }

    public CharMetrics? GetCharMetrics(Rune codePoint, float scale)
    {
        return TryGetGlyph(codePoint, scale, 0, out var metrics, out _, out _) ? metrics : null;
    }

    public int GetAscent(float scale)
    {
        var scaled = GetScaleDatum(scale);
        return scaled.Ascent;
    }

    public int GetDescent(float scale)
    {
        var scaled = GetScaleDatum(scale);
        return scaled.Descent;
    }

    public int GetHeight(float scale)
    {
        var scaled = GetScaleDatum(scale);
        return scaled.Height;
    }

    public int GetLineHeight(float scale)
    {
        var scaled = GetScaleDatum(scale);
        return scaled.LineHeight;
    }

    private uint GetGlyph(Rune chr)
    {
        if (GlyphMap.TryGetValue(chr, out var glyph))
        {
            return glyph;
        }

        // Check FreeType to see if it exists.
        var index = FaceHandle.Face.GetCharIndex((uint)chr.Value);

        GlyphMap.Add(chr, index);

        return index;
    }

    private ScaledFontData GetScaleDatum(float scale)
    {
        if (_scaledData.TryGetValue(scale, out var datum))
        {
            return datum;
        }

        datum = _fontManager._generateScaledDatum(this, scale);
        _scaledData.Add(scale, datum);
        return datum;
    }
}

internal sealed class ScaledFontData
{
    public ScaledFontData(int ascent, int descent, int height, int lineHeight, Library library)
    {
        Ascent = ascent;
        Descent = descent;
        Height = height;
        LineHeight = lineHeight;
        Stroker = new Stroker(library);
    }

    public readonly List<OwnedTexture> AtlasTextures = new();
    public readonly Dictionary<uint, GlyphInfo> GlyphInfos = new();
    public readonly Dictionary<(uint Glyph, int Radius), OutlinedGlyphInfo> OutlinedGlyphInfos = new();
    public readonly Stroker Stroker;
    public readonly int Ascent;
    public readonly int Descent;
    public readonly int Height;
    public readonly int LineHeight;

    public int CurSheetX;
    public int CurSheetY;
    public int CurSheetMaxY;
}

internal sealed class OutlinedGlyphInfo
{
    public AtlasTexture? Texture;
    public int Left;
    public int Top;
}

internal sealed class GlyphInfo
{
    public CharMetrics Metrics;
    public AtlasTexture? Texture;
}
