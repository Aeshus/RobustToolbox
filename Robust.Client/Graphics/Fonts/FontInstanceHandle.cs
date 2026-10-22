using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SharpFont;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Robust.Client.Graphics.Fonts;

/// <summary>
///     Metrics for a single glyph in a font.
///     Refer to https://www.freetype.org/freetype2/docs/glyphs/glyphs-3.html for more information.
/// </summary>
public readonly struct CharMetrics(int bearingX, int bearingY, int advance, int width, int height)
{
    /// <summary>
    ///     The horizontal distance between the origin and the left of the drawn glyph.
    /// </summary>
    public readonly int BearingX = bearingX;

    /// <summary>
    ///     The vertical distance between the origin and the top of the drawn glyph.
    /// </summary>
    public readonly int BearingY = bearingY;

    /// <summary>
    ///     How much to advance the origin after drawing the glyph.
    /// </summary>
    public readonly int Advance = advance;

    /// <summary>
    ///     The width of the drawn glyph.
    /// </summary>
    public readonly int Width = width;

    /// <summary>
    ///     The height of the drawn glyph.
    /// </summary>
    public readonly int Height = height;
}

internal readonly record struct OutlinedGlyph(Texture Texture, int Left, int Top);

[PublicAPI]
internal sealed class FontInstanceHandle(IClyde clyde, Library library, FontFaceHandle faceHandle, int size)
{
    private readonly IClyde _clyde = clyde;
    private readonly Library _library = library;

    private const float FreeType26Dot6Scale = 64f;
    private const int SheetWidth = 256;
    private const int SheetHeight = 256;

    private readonly FontFaceHandle _faceHandle = faceHandle;
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
        var glyph = _faceHandle.GetGlyphIndex(codePoint);
        if (glyph == 0)
        {
            metrics = default;
            texture = null;
            outlinedGlyph = null;
            return false;
        }

        var scaled = GetScaleDatum(scale);
        var glyphInfo = scaled.EnsureGlyphCached(scale, glyph);
        metrics = glyphInfo.Metrics;
        texture = glyphInfo.Texture;
        outlinedGlyph = null;

        // An outline is never rendered without the normal glyph, so avoid generating or
        // looking up outline data when the glyph has no bitmap.
        if (texture == null || outlineThickness <= 0)
            return true;

        var radius = (int)MathF.Round(outlineThickness * scale * FreeType26Dot6Scale);
        if (radius <= 0)
            return true;

        var outlinedInfo = scaled.EnsureOutlinedGlyphCached(scale, glyph, radius);
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

        var glyph = _faceHandle.GetGlyphIndex(codePoint);
        if (glyph == 0)
            return null;

        var radius = (int)MathF.Round(thickness * scale * FreeType26Dot6Scale);
        if (radius <= 0)
            return null;

        var scaled = GetScaleDatum(scale);
        var info = scaled.EnsureOutlinedGlyphCached(scale, glyph, radius);
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

    private FontInstance GetScaleDatum(float scale)
    {
        if (_instances.TryGetValue(scale, out var datum))
        {
            return datum;
        }

        return CreateFontInstance(this, scale);
    }

    public FontInstance CreateFontInstance(FontInstanceHandle instanceHandle, float scale)
    {
        var instance = new FontInstance(instanceHandle, scale);
        _instances.Add(scale, instance);
        return instance;
    }

    internal sealed class FontInstance
    {
        private readonly FontInstanceHandle _instanceHandle;

        public FontInstance(FontInstanceHandle handle, float scale)
        {
            var ftFace = handle._faceHandle.Face;
            VerticalResolution = (uint)(scale * handle.FontDpi);
            ftFace.SetCharSize(0, handle.Size, 0, VerticalResolution);

            Ascent = ftFace.Size.Metrics.Ascender.ToInt32();
            Descent = -ftFace.Size.Metrics.Descender.ToInt32();
            LineHeight = ftFace.Size.Metrics.Height.ToInt32();
            Height = Ascent + Descent;

            Stroker = new Stroker(handle._library);
            _instanceHandle = handle;
        }

        public readonly List<OwnedTexture> AtlasTextures = new();
        public readonly Dictionary<uint, GlyphInfo> GlyphInfos = new();
        public readonly Dictionary<(uint Glyph, int Radius), OutlinedGlyphInfo> OutlinedGlyphInfos = new();
        public readonly Stroker Stroker;
        public readonly int Ascent;
        public readonly int Descent;
        public readonly int Height;
        public readonly int LineHeight;
        public readonly uint VerticalResolution;

        public int CurSheetX;
        public int CurSheetY;
        public int CurSheetMaxY;

        internal GlyphInfo EnsureGlyphCached(
            float scale,
            uint glyph)
        {
            // Check if already cached.
            if (GlyphInfos.TryGetValue(glyph, out var info))
                return info;

            info = new GlyphInfo();

            var face = _instanceHandle._faceHandle.Face;
            face.SetCharSize(0, _instanceHandle.Size, 0, VerticalResolution);
            face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);
            face.Glyph.RenderGlyph(RenderMode.Normal);

            var glyphMetrics = face.Glyph.Metrics;
            info.Metrics = new CharMetrics(glyphMetrics.HorizontalBearingX.ToInt32(),
                glyphMetrics.HorizontalBearingY.ToInt32(),
                glyphMetrics.HorizontalAdvance.ToInt32(),
                glyphMetrics.Width.ToInt32(),
                glyphMetrics.Height.ToInt32());

            using var bitmap = face.Glyph.Bitmap;
            info.Texture = CacheBitmap(bitmap);

            GlyphInfos.Add(glyph, info);
            return info;
        }

        internal OutlinedGlyphInfo EnsureOutlinedGlyphCached(
            float scale,
            uint glyph,
            int radius)
        {
            if (OutlinedGlyphInfos.TryGetValue((glyph, radius), out var info))
                return info;

            var face = _instanceHandle._faceHandle.Face;
            face.SetCharSize(0, _instanceHandle.Size, 0, VerticalResolution);
            face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);

            using var sourceGlyph = face.Glyph.GetGlyph();
            var stroker = Stroker;
            stroker.Set(radius, StrokerLineCap.Round, StrokerLineJoin.Round, Fixed16Dot16.FromInt32(0));
            stroker.Rewind();
            using var strokedGlyph = sourceGlyph.StrokeBorder(stroker, false, true);
            strokedGlyph.ToBitmap(RenderMode.Normal, new FTVector26Dot6(0, 0), true);

            var bitmapGlyph = strokedGlyph.ToBitmapGlyph();
            using var bitmap = bitmapGlyph.Bitmap;
            info = new OutlinedGlyphInfo
            {
                Texture = CacheBitmap(bitmap),
                Left = bitmapGlyph.Left,
                Top = bitmapGlyph.Top,
            };

            OutlinedGlyphInfos.Add((glyph, radius), info);
            return info;
        }

        private AtlasTexture? CacheBitmap(
            FTBitmap bitmap)
        {
            if (bitmap.Pitch < 0)
                throw new NotImplementedException();

            if (bitmap.Pitch == 0)
                return null;

            Image<A8> img;
            switch (bitmap.PixelMode)
            {
                case PixelMode.Mono:
                    img = MonoBitMapToImage(bitmap);
                    break;
                case PixelMode.Gray:
                {
                    ReadOnlySpan<A8> span;
                    unsafe
                    {
                        span = new ReadOnlySpan<A8>((void*)bitmap.Buffer, bitmap.Pitch * bitmap.Rows);
                    }

                    img = new Image<A8>(bitmap.Width, bitmap.Rows);
                    span.Blit(
                        bitmap.Pitch,
                        UIBox2i.FromDimensions(0, 0, bitmap.Pitch, bitmap.Rows),
                        img,
                        (0, 0));
                    break;
                }
                case PixelMode.Gray2:
                case PixelMode.Gray4:
                case PixelMode.Lcd:
                case PixelMode.VerticalLcd:
                case PixelMode.Bgra:
                    throw new NotImplementedException();
                default:
                    throw new ArgumentOutOfRangeException();
            }

            OwnedTexture sheet;
            if (AtlasTextures.Count == 0)
                sheet = GenSheet();
            else
                sheet = AtlasTextures[^1];

            var (sheetW, sheetH) = sheet.Size;
            if (sheetW - CurSheetX < img.Width)
            {
                CurSheetX = 0;
                // +1 adds vertical padding to avoid artifacts when aliasing.
                CurSheetY = CurSheetMaxY + 1;
            }

            if (sheetH - CurSheetY < img.Height)
            {
                CurSheetY = 0;
                CurSheetX = 0;
                CurSheetMaxY = 0;
                sheet = GenSheet();
            }

            sheet.SetSubImage((CurSheetX, CurSheetY), img);
            var atlasTexture = new AtlasTexture(
                sheet,
                UIBox2.FromDimensions(CurSheetX, CurSheetY, bitmap.Width, bitmap.Rows));

            CurSheetMaxY = Math.Max(CurSheetMaxY, CurSheetY + bitmap.Rows);
            // +1 adds horizontal padding to avoid artifacts when aliasing.
            CurSheetX += bitmap.Width + 1;
            return atlasTexture;

            OwnedTexture GenSheet()
            {
                var face = _instanceHandle._faceHandle.Face;
                var texture = _instanceHandle._clyde.CreateBlankTexture<A8>((SheetWidth, SheetHeight),
                    $"font-{face.FamilyName}-{_instanceHandle.Size}-{VerticalResolution}-sheet{AtlasTextures.Count}");
                AtlasTextures.Add(texture);
                return texture;
            }
        }

        private static Image<A8> MonoBitMapToImage(FTBitmap bitmap)
        {
            DebugTools.Assert(bitmap.PixelMode == PixelMode.Mono);
            DebugTools.Assert(bitmap.Pitch > 0);

            ReadOnlySpan<byte> span;
            unsafe
            {
                span = new ReadOnlySpan<byte>((void*)bitmap.Buffer, bitmap.Rows * bitmap.Pitch);
            }

            var bitmapImage = new Image<A8>(bitmap.Width, bitmap.Rows);
            for (var y = 0; y < bitmap.Rows; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    var byteIndex = y * bitmap.Pitch + (x / 8);
                    var bitIndex = x % 8;

                    var bit = (span[byteIndex] & (1 << (7 - bitIndex))) != 0;
                    bitmapImage[x, y] = new A8(bit ? byte.MaxValue : byte.MinValue);
                }
            }

            return bitmapImage;
        }
    }
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
