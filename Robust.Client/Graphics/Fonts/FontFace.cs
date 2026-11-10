using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SharpFont;

namespace Robust.Client.Graphics.Fonts;

#pragma warning disable CS0618 // Type or member is obsolete
public sealed class FontFace : ISystemFontFace
#pragma warning restore CS0618 // Type or member is obsolete
{
    private const float FreeType26Dot6Scale = 64f;
    private readonly Dictionary<(int, uint), FontInstance> _cachedInstances = new();
    private readonly Dictionary<Rune, uint> _glyphIndexCache = new();

    private readonly IClyde _clyde;
    private readonly Library _library;

    private readonly Lazy<FaceHandle> _faceHandle;
    private readonly FontMetadata _metadata;

    internal FontFace(
        IClyde clyde,
        Library library,
        Func<FaceHandle> faceHandle,
        FontMetadata? metadata = null)
    {
        _clyde = clyde;
        _library = library;
        _faceHandle = new Lazy<FaceHandle>(faceHandle);
        _metadata = metadata ?? new FontMetadata(_faceHandle.Value.Face);
    }

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
        var index = _faceHandle.Value.Face.GetCharIndex((uint)chr.Value);

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

        instance = new FontInstance(_clyde, _library, _faceHandle.Value.Face, size, verticalResolution);
        _cachedInstances.Add((size, verticalResolution), instance);
        return instance;
    }

    public string PostscriptName => _metadata.PostscriptName;
    public string FullName => _metadata.FullName;
    public string FamilyName => _metadata.FamilyName;
    public string FaceName => _metadata.FaceName;
    public string GetLocalizedFullName(CultureInfo culture) => _metadata.GetLocalizedFullName(culture);
    public string GetLocalizedFamilyName(CultureInfo culture) => _metadata.GetLocalizedFamilyName(culture);
    public string GetLocalizedFaceName(CultureInfo culture) => _metadata.GetLocalizedFaceName(culture);
    public Font Load(int size) => new VectorFont(this, size);
    public FontWeight Weight => _metadata.Weight;
    public FontSlant Slant => _metadata.Slant;
    public FontWidth Width => _metadata.Width;
}

internal struct FaceHandle(Face face, IFontMemoryHandle memoryHandle)
{
    public readonly Face Face = face;

    // ReSharper disable once UnusedMember.Local
    private readonly IFontMemoryHandle _memoryHandle = memoryHandle;
}
