using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SharpFont;
using SharpFont.TrueType;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontMetadata
{
    private const int ItalicFlag = 1 << 0;
    private const int ObliqueFlag = 1 << 9;

    public required string PostscriptName { get; init; }

    public required LocalizedStringSet FullNames = LocalizedStringSet.Empty;
    public required LocalizedStringSet FamilyNames = LocalizedStringSet.Empty;
    public required LocalizedStringSet FaceNames = LocalizedStringSet.Empty;

    public required FontWeight Weight { get; init; }
    public required FontSlant Slant { get; init; }
    public required FontWidth Width { get; init; }

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

    [SetsRequiredMembers]
    public FontMetadata(Face face)
    {
        PostscriptName = face.GetPostscriptName();

        if (!face.IsSfnt || face.GetSfntTable(SfntTag.OS2) is not OS2 os2)
        {
            // Defaults
            Weight = FontWeight.Normal;
            Slant = FontSlant.Normal;
            Width = FontWidth.Normal;
            return;
        }

        var flags = os2.SelectionFlags;

        FontSlant slant;
        if (os2.Version >= 4 && (flags & ObliqueFlag) != 0)
            slant = FontSlant.Oblique;
        else
            slant = (flags & ItalicFlag) != 0 ? FontSlant.Italic : FontSlant.Normal;

        Weight = (FontWeight)os2.WeightClass;
        Width = (FontWidth)os2.WidthClass;
        Slant = slant;
    }

    public FontMetadata()
    {
    }
}
