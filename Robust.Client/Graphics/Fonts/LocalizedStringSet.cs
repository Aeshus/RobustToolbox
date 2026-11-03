using System.Collections.Generic;

namespace Robust.Client.Graphics.Fonts;

internal struct LocalizedStringSet
{
    public static readonly LocalizedStringSet Empty = FromSingle("");

    /// <summary>
    /// The first locale to appear in the list of localized strings.
    /// Used as fallback if the desired locale is not provided.
    /// </summary>
    public required string Primary;
    public required Dictionary<string, string> Values;

    public static LocalizedStringSet FromSingle(string value, string language = "en")
    {
        return new LocalizedStringSet
        {
            Primary = language,
            Values = new Dictionary<string, string> { { language, value } }
        };
    }
}
