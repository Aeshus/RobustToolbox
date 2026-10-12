using System;
using System.Collections.Generic;
using System.IO;
using Robust.Client.Utility;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SharpFont;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Robust.Client.Graphics.Fonts;

internal sealed partial class FontManager : IFontManagerInternal, IPostInjectInit
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private ILogManager _logManager = default!;

    private const int SheetWidth = 256;

    private const int SheetHeight = 256;

    // FreeType's stroker radius is expressed in 26.6 fixed-point pixel units.
    internal const float FreeType26Dot6Scale = 64f;

    private ISawmill _sawmill = default!;

    private uint _baseFontDpi = 96;

    private readonly Library _library = new();

    private readonly Dictionary<(FontFaceHandle, int fontSize), FontInstanceHandle> _loadedInstances =
        new();

    public void PostInject()
    {
        _sawmill = _logManager.GetSawmill("font");
    }

    public IFontFaceHandle Load(Stream stream, int index = 0)
    {
        // Freetype directly operates on the font memory managed by us.
        // As such, the font data should be pinned in POH.
        var fontData = stream.CopyToPinnedArray();
        return Load(new ArrayFontMemoryHandle(fontData), index);
    }

    public IFontFaceHandle Load(IFontMemoryHandle memory, int index = 0)
    {
        var face = FaceLoad(memory, index);
        var handle = new FontFaceHandle(face, memory);
        return handle;
    }

    public IFontFaceHandle LoadWithPostscriptName(IFontMemoryHandle memory, string postscriptName)
    {
        var numFaces = 1;

        for (var i = 0; i < numFaces; i++)
        {
            var face = FaceLoad(memory, i);
            numFaces = face.FaceCount;

            if (face.GetPostscriptName() == postscriptName)
                return new FontFaceHandle(face, memory);

            face.Dispose();
        }

        // Fallback, load SOMETHING.
        _sawmill.Warning($"Failed to load correct font via postscript name! {postscriptName}");
        return new FontFaceHandle(FaceLoad(memory, 0), memory);
    }

    private unsafe Face FaceLoad(IFontMemoryHandle memory, int index)
    {
        return new Face(_library,
            (nint)memory.GetData(),
            checked((int)memory.GetDataSize()),
            index);
    }

    void IFontManagerInternal.SetFontDpi(uint fontDpi)
    {
        _baseFontDpi = fontDpi;
    }

    public IFontInstanceHandle MakeInstance(IFontFaceHandle handle, int size)
    {
        var fontFaceHandle = (FontFaceHandle)handle;
        if (_loadedInstances.TryGetValue((fontFaceHandle, size), out var instance))
        {
            return instance;
        }

        instance = new FontInstanceHandle(this, size, fontFaceHandle);

        _loadedInstances.Add((fontFaceHandle, size), instance);
        return instance;
    }

    public void ClearFontCache()
    {
        foreach (var fontInstance in _loadedInstances)
        {
            fontInstance.Value.ClearSizeData();
        }
    }

    internal ScaledFontData _generateScaledDatum(FontInstanceHandle instance, float scale)
    {
        var ftFace = instance.FaceHandle.Face;
        ftFace.SetCharSize(0, instance.Size, 0, (uint)(_baseFontDpi * scale));

        var ascent = ftFace.Size.Metrics.Ascender.ToInt32();
        var descent = -ftFace.Size.Metrics.Descender.ToInt32();
        var lineHeight = ftFace.Size.Metrics.Height.ToInt32();

        var data = new ScaledFontData(ascent, descent, ascent + descent, lineHeight, _library);


        return data;
    }

    internal GlyphInfo EnsureGlyphCached(FontInstanceHandle instance, ScaledFontData scaled, float scale, uint glyph)
    {
        // Check if already cached.
        if (scaled.GlyphInfos.TryGetValue(glyph, out var info))
            return info;

        info = new GlyphInfo();

        var face = instance.FaceHandle.Face;
        face.SetCharSize(0, instance.Size, 0, (uint)(_baseFontDpi * scale));
        face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);
        face.Glyph.RenderGlyph(RenderMode.Normal);

        var glyphMetrics = face.Glyph.Metrics;
        info.Metrics = new CharMetrics(glyphMetrics.HorizontalBearingX.ToInt32(),
            glyphMetrics.HorizontalBearingY.ToInt32(),
            glyphMetrics.HorizontalAdvance.ToInt32(),
            glyphMetrics.Width.ToInt32(),
            glyphMetrics.Height.ToInt32());

        using var bitmap = face.Glyph.Bitmap;
        info.Texture = CacheBitmap(instance, scaled, scale, bitmap);

        scaled.GlyphInfos.Add(glyph, info);
        return info;
    }

    internal OutlinedGlyphInfo EnsureOutlinedGlyphCached(
        FontInstanceHandle instance,
        ScaledFontData scaled,
        float scale,
        uint glyph,
        int radius)
    {
        if (scaled.OutlinedGlyphInfos.TryGetValue((glyph, radius), out var info))
            return info;

        var face = instance.FaceHandle.Face;
        face.SetCharSize(0, instance.Size, 0, (uint)(_baseFontDpi * scale));
        face.LoadGlyph(glyph, LoadFlags.Default, LoadTarget.Normal);

        using var sourceGlyph = face.Glyph.GetGlyph();
        var stroker = scaled.Stroker;
        stroker.Set(radius, StrokerLineCap.Round, StrokerLineJoin.Round, Fixed16Dot16.FromInt32(0));
        stroker.Rewind();
        using var strokedGlyph = sourceGlyph.StrokeBorder(stroker, false, true);
        strokedGlyph.ToBitmap(RenderMode.Normal, new FTVector26Dot6(0, 0), true);

        var bitmapGlyph = strokedGlyph.ToBitmapGlyph();
        using var bitmap = bitmapGlyph.Bitmap;
        info = new OutlinedGlyphInfo
        {
            Texture = CacheBitmap(instance, scaled, scale, bitmap),
            Left = bitmapGlyph.Left,
            Top = bitmapGlyph.Top,
        };

        scaled.OutlinedGlyphInfos.Add((glyph, radius), info);
        return info;
    }

    private AtlasTexture? CacheBitmap(
        FontInstanceHandle instance,
        ScaledFontData scaled,
        float scale,
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
        if (scaled.AtlasTextures.Count == 0)
            sheet = GenSheet();
        else
            sheet = scaled.AtlasTextures[^1];

        var (sheetW, sheetH) = sheet.Size;
        if (sheetW - scaled.CurSheetX < img.Width)
        {
            scaled.CurSheetX = 0;
            // +1 adds vertical padding to avoid artifacts when aliasing.
            scaled.CurSheetY = scaled.CurSheetMaxY + 1;
        }

        if (sheetH - scaled.CurSheetY < img.Height)
        {
            scaled.CurSheetY = 0;
            scaled.CurSheetX = 0;
            scaled.CurSheetMaxY = 0;
            sheet = GenSheet();
        }

        sheet.SetSubImage((scaled.CurSheetX, scaled.CurSheetY), img);
        var atlasTexture = new AtlasTexture(
            sheet,
            UIBox2.FromDimensions(scaled.CurSheetX, scaled.CurSheetY, bitmap.Width, bitmap.Rows));

        scaled.CurSheetMaxY = Math.Max(scaled.CurSheetMaxY, scaled.CurSheetY + bitmap.Rows);
        // +1 adds horizontal padding to avoid artifacts when aliasing.
        scaled.CurSheetX += bitmap.Width + 1;
        return atlasTexture;

        OwnedTexture GenSheet()
        {
            var face = instance.FaceHandle.Face;
            var texture = _clyde.CreateBlankTexture<A8>((SheetWidth, SheetHeight),
                $"font-{face.FamilyName}-{instance.Size}-{(uint)(_baseFontDpi * scale)}-sheet{scaled.AtlasTextures.Count}");
            scaled.AtlasTextures.Add(texture);
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
