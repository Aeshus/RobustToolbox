using System;
using System.Globalization;
using SharpFont;
using SharpFont.TrueType;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontMetadata
{
    private const int ItalicFlag = 1 << 0;
    private const int ObliqueFlag = 1 << 9;

    public string PostscriptName { get; init; }

    public LocalizedStringSet FullNames;
    public LocalizedStringSet FamilyNames;
    public LocalizedStringSet FaceNames;

    public FontWeight Weight { get; init; }
    public FontSlant Slant { get; init; }
    public FontWidth Width { get; init; }

    public string FullName => GetLocalizedFullName(CultureInfo.CurrentCulture);
    public string FamilyName => GetLocalizedFamilyName(CultureInfo.CurrentCulture);
    public string FaceName => GetLocalizedFaceName(CultureInfo.CurrentCulture);

    public string GetLocalizedFullName(CultureInfo culture)
    {
        return FullNames.GetLocalized(culture);
    }

    public string GetLocalizedFamilyName(CultureInfo culture)
    {
        return FamilyNames.GetLocalized(culture);
    }

    public string GetLocalizedFaceName(CultureInfo culture)
    {
        return FaceNames.GetLocalized(culture);
    }

    public FontMetadata(Face face)
    {
        if (!face.IsSfnt)
            throw new InvalidOperationException($"Font '{face.FamilyName}' does not have an SFNT table");

        if (face.GetSfntTable(SfntTag.OS2) is not OS2 os2)
            throw new InvalidOperationException($"Font '{face.FamilyName}' does not have an OS/2 table");

        var flags = os2.SelectionFlags;

        FontSlant slant;
        if (os2.Version >= 4 && (flags & ObliqueFlag) != 0)
            slant = FontSlant.Oblique;
        else
            slant = (flags & ItalicFlag) != 0 ? FontSlant.Italic : FontSlant.Normal;

        PostscriptName = face.GetPostscriptName();
        Weight = (FontWeight)os2.WeightClass;
        Width = (FontWidth)os2.WidthClass;
        Slant = slant;
    }
}
