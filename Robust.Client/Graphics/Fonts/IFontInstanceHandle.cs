using System.Text;

namespace Robust.Client.Graphics.Fonts;

internal interface IFontInstanceHandle
{
    /// <summary>
    ///     Fetches all data needed to draw a glyph.
    /// </summary>
    bool TryGetGlyph(
        Rune codePoint,
        float scale,
        float outlineThickness,
        out CharMetrics metrics,
        out Texture? texture,
        out OutlinedGlyph? outlinedGlyph);

    Texture? GetCharTexture(Rune codePoint, float scale);
    Texture? GetCharTexture(char chr, float scale) => GetCharTexture((Rune)chr, scale);
    OutlinedGlyph? GetOutlinedChar(Rune codePoint, float scale, float thickness);
    CharMetrics? GetCharMetrics(Rune codePoint, float scale);
    CharMetrics? GetCharMetrics(char chr, float scale) => GetCharMetrics((Rune)chr, scale);

    int GetAscent(float scale);
    int GetDescent(float scale);
    int GetHeight(float scale);
    int GetLineHeight(float scale);
}
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

