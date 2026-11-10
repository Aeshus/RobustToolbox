using System;
using System.IO;
using Robust.Client.Graphics.Fonts;

namespace Robust.Client.Graphics;

[NotContentImplementable]
public interface IFontManager
{
    public void ClearFontCache();
}

internal interface IFontManagerInternal : IFontManager
{
    FontFace Load(Stream stream, int index, FontMetadata? metadata = null);
    FontFace Load(IFontMemoryHandle memory, int index, FontMetadata? metadata = null);

    FontFace LoadWithPostscriptName(
        IFontMemoryHandle memory,
        string postscriptName,
        FontMetadata? metadata = null);

    uint FontDpi { set; }
}

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
