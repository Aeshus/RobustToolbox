using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Robust.Shared.Log;

namespace Robust.Client.Graphics.Fonts;

internal abstract class SystemFontManagerBase
{
    /// <summary>
    /// The "standard" locale used when looking up the PostScript name of a font face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Font files allow the PostScript name to be localized, however in practice
    /// we would really like to have a language-unambiguous identifier to refer to a font file.
    /// We use this locale (en-US) to look up teh PostScript font name, if there are multiple provided.
    /// This matches the behavior of the Local Font Access web API:
    /// https://wicg.github.io/local-font-access/#concept-font-representation
    /// </para>
    /// </remarks>
    protected static readonly CultureInfo StandardLocale = new("en-US", false);

    protected readonly IFontManagerInternal FontManager;
    protected readonly ISawmill Sawmill;

    protected readonly Lock Lock = new();
    protected readonly List<FontFace> Fonts = [];

    public IEnumerable<FontFace> SystemFontFaces { get; }

    public SystemFontManagerBase(ILogManager logManager, IFontManagerInternal fontManager)
    {
        FontManager = fontManager;
        Sawmill = logManager.GetSawmill("font.system");

        SystemFontFaces = Fonts.AsReadOnly();
    }

    protected static string GetLocalizedForLocaleOrFirst(LocalizedStringSet set, CultureInfo culture)
    {
        var matchCulture = culture;
        while (!Equals(matchCulture, CultureInfo.InvariantCulture))
        {
            if (set.Values.TryGetValue(culture.Name, out var value))
                return value;

            matchCulture = matchCulture.Parent;
        }

        return set.Values[set.Primary];
    }
}
