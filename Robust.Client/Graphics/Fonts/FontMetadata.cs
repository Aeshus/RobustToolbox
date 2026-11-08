using System.Globalization;

namespace Robust.Client.Graphics.Fonts;

internal sealed class FontMetadata
{
    public required string PostscriptName { get; init; }

    public LocalizedStringSet FullNames;
    public LocalizedStringSet FamilyNames;
    public LocalizedStringSet FaceNames;

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
}
