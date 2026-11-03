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
    protected readonly List<BaseHandle> Fonts = [];

    public IEnumerable<ISystemFontFace> SystemFontFaces { get; }

    public SystemFontManagerBase(ILogManager logManager, IFontManagerInternal fontManager)
    {
        FontManager = fontManager;
        Sawmill = logManager.GetSawmill("font.system");

        SystemFontFaces = Fonts.AsReadOnly();
    }

    protected abstract FontFace LoadFontFace(BaseHandle handle);

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

    protected abstract class BaseHandle(SystemFontManagerBase parent) : ISystemFontFace
    {
        private FontFace? _cachedFont;

        public required string PostscriptName { get; init; }

        public required LocalizedStringSet FullNames;
        public required LocalizedStringSet FamilyNames;
        public required LocalizedStringSet FaceNames;

        public required FontWeight Weight { get; init; }
        public required FontSlant Slant { get; init; }
        public required FontWidth Width { get; init; }

        public string FullName => GetLocalizedFullName(CultureInfo.CurrentCulture);
        public string FamilyName => GetLocalizedFamilyName(CultureInfo.CurrentCulture);
        public string FaceName => GetLocalizedFaceName(CultureInfo.CurrentCulture);

        public string GetLocalizedFullName(CultureInfo culture)
        {
            return GetLocalizedForLocaleOrFirst(FullNames, culture);
        }

        public string GetLocalizedFamilyName(CultureInfo culture)
        {
            return GetLocalizedForLocaleOrFirst(FamilyNames, culture);
        }

        public string GetLocalizedFaceName(CultureInfo culture)
        {
            return GetLocalizedForLocaleOrFirst(FaceNames, culture);
        }

        public Font Load(int size)
        {
            var handle = GetFaceHandle();

            return new VectorFont(handle, size);
        }

        private FontFace GetFaceHandle()
        {
            lock (parent.Lock)
            {
                if (_cachedFont != null)
                    return _cachedFont;

                parent.Sawmill.Verbose($"Loading system font face: {PostscriptName}");

                return _cachedFont = parent.LoadFontFace(this);
            }
        }
    }
}
