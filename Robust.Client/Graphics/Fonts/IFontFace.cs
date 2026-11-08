using System;
using System.Globalization;

namespace Robust.Client.Graphics.Fonts;

public interface IFontFace
{
    /// <summary>
    /// The PostScript name of the font face.
    /// This is generally the closest to an unambiguous unique identifier as you're going to get.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, "Arial-ItalicMT"
    /// </para>
    /// </remarks>
    string PostscriptName { get; }

    /// <summary>
    /// The full name of the font face, localized to the current locale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, "Arial Cursiva"
    /// </para>
    /// </remarks>
    /// <seealso cref="GetLocalizedFullName"/>
    string FullName { get; }

    /// <summary>
    /// The family name of the font face, localized to the current locale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, "Arial"
    /// </para>
    /// </remarks>
    /// <seealso cref="GetLocalizedFamilyName"/>
    string FamilyName { get; }

    /// <summary>
    /// The face name (or "style name") of the font face, localized to the current locale.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For example, "Cursiva"
    /// </para>
    /// </remarks>
    /// <seealso cref="GetLocalizedFaceName"/>
    string FaceName { get; }

    /// <summary>
    /// Get the <see cref="FullName"/>, localized to a specific locale.
    /// </summary>
    /// <param name="culture">The locale to fetch the localized string for.</param>
    string GetLocalizedFullName(CultureInfo culture);

    /// <summary>
    /// Get the <see cref="FamilyName"/>, localized to a specific locale.
    /// </summary>
    /// <param name="culture">The locale to fetch the localized string for.</param>
    string GetLocalizedFamilyName(CultureInfo culture);

    /// <summary>
    /// Get the <see cref="FaceName"/>, localized to a specific locale.
    /// </summary>
    /// <param name="culture">The locale to fetch the localized string for.</param>
    string GetLocalizedFaceName(CultureInfo culture);

    /// <summary>
    /// The weight of the font face.
    /// </summary>
    FontWeight Weight { get; }

    /// <summary>
    /// The slant of the font face.
    /// </summary>
    FontSlant Slant { get; }

    /// <summary>
    /// The width of the font face.
    /// </summary>
    FontWidth Width { get; }
}
