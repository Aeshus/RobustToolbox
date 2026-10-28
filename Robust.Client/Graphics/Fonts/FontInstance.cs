using System;
using System.Collections.Generic;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SharpFont;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontInstance : IDisposable
{
    private const int SheetWidth = 256;
    private const int SheetHeight = 256;

    public readonly int Ascent;
    public readonly int Descent;
    public readonly int Height;
    public readonly int LineHeight;

    private readonly List<OwnedTexture> _atlasTextures = new();
    private readonly IClyde _clyde;
    private readonly Face _face;
    private readonly Dictionary<uint, GlyphInfo> _glyphInfos = new();
    private readonly Dictionary<(uint Glyph, int Radius), OutlinedGlyphInfo> _outlinedGlyphInfos = new();
    private readonly int _size;
    private readonly Stroker _stroker;
    private readonly uint _verticalResolution;
    private int _curSheetMaxY;

    private int _curSheetX;
    private int _curSheetY;

    public FontInstance(IClyde clyde, Library library, Face face, int size, uint verticalResolution)
    {
        var ftFace = face;
        _verticalResolution = verticalResolution;
        ftFace.SetCharSize(0, size, 0, _verticalResolution);

        Ascent = ftFace.Size.Metrics.Ascender.ToInt32();
        Descent = -ftFace.Size.Metrics.Descender.ToInt32();
        LineHeight = ftFace.Size.Metrics.Height.ToInt32();
        Height = Ascent + Descent;

        _stroker = new Stroker(library);
        _clyde = clyde;
        _face = face;
        _size = size;
    }

    public void Dispose()
    {
        foreach (var texture in _atlasTextures)
        {
            texture.Dispose();
        }

        _stroker.Dispose();
    }

    internal GlyphInfo RenderGlyph(uint glyph)
    {
        // Check if already cached.
        if (_glyphInfos.TryGetValue(glyph, out var info))
            return info;

        info = new GlyphInfo();

        var face = _face;
        face.SetCharSize(0, _size, 0, _verticalResolution);
        face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);
        face.Glyph.RenderGlyph(RenderMode.Normal);

        var glyphMetrics = face.Glyph.Metrics;
        info.Metrics = new CharMetrics(glyphMetrics.HorizontalBearingX.ToInt32(),
            glyphMetrics.HorizontalBearingY.ToInt32(),
            glyphMetrics.HorizontalAdvance.ToInt32(),
            glyphMetrics.Width.ToInt32(),
            glyphMetrics.Height.ToInt32());

        using var bitmap = face.Glyph.Bitmap;
        info.Texture = SaveBitmap(bitmap);

        _glyphInfos.Add(glyph, info);
        return info;
    }

    internal OutlinedGlyphInfo RenderOutlineGlyph(uint glyph, int radius)
    {
        if (_outlinedGlyphInfos.TryGetValue((glyph, radius), out var info))
            return info;

        var face = _face;
        face.SetCharSize(0, _size, 0, _verticalResolution);
        face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);

        using var sourceGlyph = face.Glyph.GetGlyph();
        var stroker = _stroker;
        stroker.Set(radius, StrokerLineCap.Round, StrokerLineJoin.Round, Fixed16Dot16.FromInt32(0));
        stroker.Rewind();
        using var strokedGlyph = sourceGlyph.StrokeBorder(stroker, false, true);
        strokedGlyph.ToBitmap(RenderMode.Normal, new FTVector26Dot6(0, 0), true);

        var bitmapGlyph = strokedGlyph.ToBitmapGlyph();
        using var bitmap = bitmapGlyph.Bitmap;
        info = new OutlinedGlyphInfo
        {
            Texture = SaveBitmap(bitmap),
            Left = bitmapGlyph.Left,
            Top = bitmapGlyph.Top,
        };

        _outlinedGlyphInfos.Add((glyph, radius), info);
        return info;
    }

    private AtlasTexture? SaveBitmap(FTBitmap bitmap)
    {
        if (bitmap.Pitch < 0)
            throw new NotImplementedException();

        if (bitmap.Pitch == 0)
            return null;

        Image<A8> img;
        switch (bitmap.PixelMode)
        {
            case PixelMode.Mono:
                img = MonoBitmapToImage(bitmap);
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
        if (_atlasTextures.Count == 0)
            sheet = GenSheet();
        else
            sheet = _atlasTextures[^1];

        var (sheetW, sheetH) = sheet.Size;
        if (sheetW - _curSheetX < img.Width)
        {
            _curSheetX = 0;
            // +1 adds vertical padding to avoid artifacts when aliasing.
            _curSheetY = _curSheetMaxY + 1;
        }

        if (sheetH - _curSheetY < img.Height)
        {
            _curSheetY = 0;
            _curSheetX = 0;
            _curSheetMaxY = 0;
            sheet = GenSheet();
        }

        sheet.SetSubImage((_curSheetX, _curSheetY), img);
        var atlasTexture = new AtlasTexture(
            sheet,
            UIBox2.FromDimensions(_curSheetX, _curSheetY, bitmap.Width, bitmap.Rows));

        _curSheetMaxY = Math.Max(_curSheetMaxY, _curSheetY + bitmap.Rows);
        // +1 adds horizontal padding to avoid artifacts when aliasing.
        _curSheetX += bitmap.Width + 1;
        return atlasTexture;

        OwnedTexture GenSheet()
        {
            var face = _face;
            var texture = _clyde.CreateBlankTexture<A8>(
                (SheetWidth, SheetHeight),
                $"font-{face.FamilyName}-{_size}-{_verticalResolution}-sheet{_atlasTextures.Count}");
            _atlasTextures.Add(texture);
            return texture;
        }
    }

    private static Image<A8> MonoBitmapToImage(FTBitmap bitmap)
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

    public OutlinedGlyph? RenderGlyphWithOutline(uint glyph, int radius)
    {
        if (radius <= 0)
            return null;

        var info = RenderOutlineGlyph(glyph, radius);
        if (info.Texture == null)
            return null;

        return new OutlinedGlyph(info.Texture, info.Left, info.Top);
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

internal readonly record struct OutlinedGlyph(Texture Texture, int Left, int Top);
