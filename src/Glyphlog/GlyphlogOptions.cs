using Microsoft.Extensions.Logging.Console;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Glyphlog;

/// <summary>
/// Options of <see cref="GlyphlogFormatter"/>. Timestamp, UTC and scopes come from
/// <see cref="ConsoleFormatterOptions"/>; everything here binds from <c>Logging:Console:FormatterOptions</c>.
/// </summary>
public sealed class GlyphlogOptions : ConsoleFormatterOptions {
    /// <summary>
    /// Icon of an informational log, by category. A key without <c>*</c> is a prefix on whole namespace segments
    /// (<c>Microsoft.AspNetCore</c> matches <c>Microsoft.AspNetCore.Routing</c>, not <c>Microsoft.AspNetCoreX</c>); a key
    /// with <c>*</c> is a pattern over the whole category, each <c>*</c> standing for any run of characters
    /// (<c>*Weather*</c> matches <c>MyApp.Services.WeatherService</c>, <c>*.Engine.*</c> anything under an
    /// <c>Engine</c> namespace). When several match, the one with the most literal characters wins, so the order of
    /// the entries never matters: <c>MyApp*Npc*</c> beats <c>MyApp</c>, which beats <c>*Npc*</c>.
    /// <para>
    /// Comes with the framework categories filled in; add the project's with <see cref="Map"/> or from configuration
    /// (<c>FormatterOptions:Categories</c>). Icons that come from configuration are not checked by <see cref="Map"/>'s
    /// rule.
    /// </para>
    /// </summary>
    public Dictionary<string, string> Categories { get; } = new(StringComparer.Ordinal) {
        ["Microsoft.EntityFrameworkCore"] = "💾",
        ["Microsoft.AspNetCore"] = "🌐",
        ["Microsoft.AspNetCore.Authentication"] = "🔑",
        ["Microsoft.AspNetCore.Authorization"] = "🔑",
        ["Microsoft.Extensions.Http"] = "🌐",
        ["System.Net.Http"] = "🌐",
        ["Microsoft.Hosting.Lifetime"] = "🚀",
    };

    /// <summary>Icon of an informational log whose category matches no prefix in <see cref="Categories"/>.</summary>
    public string DefaultIcon { get; set; } = "💬";

    /// <summary>Maps each of <paramref name="categories"/> to <paramref name="icon"/>, replacing what the key had.</summary>
    /// <param name="icon">A single emoji that terminals draw in two cells (🚧 💾 💬).</param>
    /// <param name="categories">Category prefixes, or patterns with <c>*</c> — see <see cref="Categories"/>.</param>
    /// <returns>These options, to chain calls.</returns>
    /// <exception cref="ArgumentException">The icon is not a single emoji, or carries the <c>U+FE0F</c> variation
    /// selector (⚠️ 🗄️ ℹ️) — those take one terminal cell instead of two and shift the line.</exception>
    public GlyphlogOptions Map(string icon, params string[] categories) {
        if (!IsAligned(icon)) {
            throw new ArgumentException(
                $"'{icon}' is not a single emoji. Use one without the U+FE0F variation selector (🚧 instead of ⚠️): "
                + "emoji with it take one terminal cell instead of two and break the column alignment.", nameof(icon));
        }
        foreach (string category in categories) {
            ArgumentException.ThrowIfNullOrWhiteSpace(category, nameof(categories));
            Categories[category] = icon;
        }
        return this;
    }

    /// <summary>
    /// Whether <paramref name="icon"/> is a single symbol code point — the shape terminals draw in two cells, which keeps
    /// every message starting on the same column. It refuses the case that actually bites: an emoji followed by the
    /// variation selector <c>U+FE0F</c> drops to one cell. It cannot tell a text-presentation emoji typed without the
    /// selector (a bare 🗄), which also takes one cell — that one is on the caller.
    /// </summary>
    internal static bool IsAligned(string? icon) =>
        !string.IsNullOrEmpty(icon)
        && Rune.DecodeFromUtf16(icon, out Rune rune, out int length) == OperationStatus.Done
        && length == icon.Length
        && Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol;
}
