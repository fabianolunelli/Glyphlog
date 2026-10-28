using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Glyphlog;

/// <summary>
/// Console formatter that writes <b>one line per log</b>, led by an emoji, in place of the default formatter's
/// two-line header (<c>info: Category[EventId]</c> + indented message). Registered by
/// <see cref="LoggingBuilderExtensions.AddGlyphlog"/>.
/// <para>
/// The emoji comes from the <b>level</b> (critical, error, warning, debug/trace) or, for informational logs, from the
/// logger <b>category</b> (<see cref="GlyphlogOptions.Categories"/>) — so a request log (🌐) stands apart from an EF
/// one (💾) without reading the full category on every line. From warning up, the short category and the event id
/// show dimmed: that is what you need to trace the log back to its source or to filter it out.
/// </para>
/// <para>
/// ANSI colors are written only when stdout is a terminal: with redirected output (a container's log output, a cloud
/// log stream, a file) the escape codes would end up as garbage in the text.
/// </para>
/// </summary>
public sealed class GlyphlogFormatter : ConsoleFormatter, IDisposable {
    /// <summary>Name used in <c>ConsoleLoggerOptions.FormatterName</c>.</summary>
    public const string FormatterName = "glyphlog";

    private const string Reset = "\x1b[0m";
    private const string Dim = "\x1b[90m";
    private const string Yellow = "\x1b[33m";
    private const string Red = "\x1b[31m";
    private const string BoldRed = "\x1b[1;31m";

    private readonly bool _color;
    private readonly IDisposable? _reload;
    private Settings _settings;

    /// <summary>Created by the console logger provider — see <see cref="LoggingBuilderExtensions.AddGlyphlog"/>.</summary>
    public GlyphlogFormatter(IOptionsMonitor<GlyphlogOptions> options) : this(options, !Console.IsOutputRedirected) { }

    internal GlyphlogFormatter(IOptionsMonitor<GlyphlogOptions> options, bool color) : base(FormatterName) {
        _color = color;
        // CurrentValue (the default name), not Get(FormatterName): the default name is where AddConsoleFormatter
        // binds the Logging:Console:FormatterOptions section of appsettings.
        _settings = new Settings(options.CurrentValue);
        _reload = options.OnChange(o => _settings = new Settings(o));
    }

    /// <inheritdoc />
    public void Dispose() => _reload?.Dispose();

    /// <inheritdoc />
    public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter) {
        string message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception) ?? string.Empty;
        if (string.IsNullOrEmpty(message) && logEntry.Exception is null) return;

        Settings settings = _settings;
        GlyphlogOptions options = settings.Options;
        string category = logEntry.Category ?? string.Empty;
        LogLevel level = logEntry.LogLevel;

        if (!string.IsNullOrEmpty(options.TimestampFormat)) {
            DateTimeOffset now = options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
            Colored(textWriter, Dim, now.ToString(options.TimestampFormat, CultureInfo.InvariantCulture));
            textWriter.Write(' ');
        }

        // A message that brings its own glyph — a summary panel, a banner logged through ILogger — goes out as is,
        // without a second icon. Only below warning: from there on the level icon is the point of the line.
        bool ownGlyph = level < LogLevel.Warning && StartsWithGlyph(message);
        if (!ownGlyph) {
            textWriter.Write(Icon(settings, category, level));
            textWriter.Write("  ");
        }

        // Warning+ carries its origin: Category[EventId] is how a log is traced or filtered at the source.
        if (level >= LogLevel.Warning) {
            string origin = logEntry.EventId.Id != 0
                ? $"{ShortCategory(category)}[{logEntry.EventId.Id}]"
                : ShortCategory(category);
            Colored(textWriter, Dim, origin);
            textWriter.Write(' ');
        }

        Colored(textWriter, LevelColor(level), ownGlyph ? message : Flatten(message));
        textWriter.WriteLine();

        if (logEntry.Exception is not null) {
            Colored(textWriter, Red, "    " + Indent(logEntry.Exception.ToString()));
            textWriter.WriteLine();
        }

        if (options.IncludeScopes && scopeProvider is not null) {
            scopeProvider.ForEachScope(static (scope, state) => {
                string? text = scope?.ToString();
                if (!string.IsNullOrEmpty(text)) {
                    state.Formatter.Colored(state.Writer, Dim, "    ↳ " + text);
                    state.Writer.WriteLine();
                }
            }, (Formatter: this, Writer: textWriter));
        }
    }

    private static string Icon(Settings settings, string category, LogLevel level) => level switch {
        LogLevel.Critical => "🔥",
        LogLevel.Error => "❌",
        LogLevel.Warning => "🚧",
        LogLevel.Debug or LogLevel.Trace => "🔍",
        _ => settings.CategoryIcon(category)
    };

    private static string LevelColor(LogLevel level) => level switch {
        LogLevel.Critical => BoldRed,
        LogLevel.Error => Red,
        LogLevel.Warning => Yellow,
        LogLevel.Debug or LogLevel.Trace => Dim,
        _ => string.Empty
    };

    private void Colored(TextWriter writer, string color, string text) {
        bool colored = _color && color.Length > 0;
        if (colored) writer.Write(color);
        writer.Write(text);
        if (colored) writer.Write(Reset);
    }

    /// <summary>
    /// Whether the first visible character is a symbol (<see cref="UnicodeCategory.OtherSymbol"/>: emoji, dingbats) —
    /// letters of any script, digits and punctuation are not.
    /// </summary>
    private static bool StartsWithGlyph(string message) =>
        Rune.DecodeFromUtf16(message.AsSpan().TrimStart(), out Rune first, out _) == OperationStatus.Done
        && Rune.GetUnicodeCategory(first) == UnicodeCategory.OtherSymbol;

    /// <summary>A multi-line message becomes a block indented under the text, so the emoji column stays aligned.</summary>
    private static string Flatten(string message) =>
        message.Contains('\n') ? Indent(message) : message;

    private static string Indent(string text) =>
        text.ReplaceLineEndings("\n").Replace("\n", "\n    ");

    private static string ShortCategory(string category) {
        int last = category.LastIndexOf('.');
        return last >= 0 && last < category.Length - 1 ? category[(last + 1)..] : category;
    }

    /// <summary>The options plus the category rules, most specific first, rebuilt only when the options change.</summary>
    private sealed class Settings(GlyphlogOptions options) {
        private readonly CategoryRule[] _rules = [.. options.Categories
            .Select(x => new CategoryRule(x.Key, x.Value))
            .OrderByDescending(x => x.Specificity)
            .ThenBy(x => x.Pattern, StringComparer.Ordinal)];

        public GlyphlogOptions Options { get; } = options;

        public string CategoryIcon(string category) {
            foreach (CategoryRule rule in _rules) {
                if (rule.Matches(category)) return rule.Icon;
            }
            return Options.DefaultIcon;
        }
    }

    /// <summary>
    /// One entry of <see cref="GlyphlogOptions.Categories"/>. Without <c>*</c> it is a prefix on whole segments:
    /// <c>Microsoft.AspNetCore</c> matches <c>Microsoft.AspNetCore.Routing</c>, not <c>Microsoft.AspNetCoreX</c>. With
    /// <c>*</c> it is a pattern over the whole category, each <c>*</c> standing for any run of characters:
    /// <c>*Weather*</c> matches <c>MyApp.Services.WeatherService</c>. Specificity is the count of literal characters,
    /// so the most specific entry wins whatever the order the entries were declared in.
    /// </summary>
    private sealed class CategoryRule(string pattern, string icon) {
        private readonly string[] _parts = pattern.Split('*');

        public string Pattern { get; } = pattern;
        public string Icon { get; } = icon;
        public int Specificity => Pattern.Length - (_parts.Length - 1);

        public bool Matches(string category) {
            if (_parts.Length == 1) {
                return category.StartsWith(Pattern, StringComparison.Ordinal)
                    && (category.Length == Pattern.Length || category[Pattern.Length] == '.');
            }

            string first = _parts[0];
            string last = _parts[^1];
            if (category.Length < first.Length + last.Length
                || !category.StartsWith(first, StringComparison.Ordinal)
                || !category.EndsWith(last, StringComparison.Ordinal)) return false;

            int position = first.Length;
            int end = category.Length - last.Length;
            for (int i = 1; i < _parts.Length - 1; i++) {
                int found = category.IndexOf(_parts[i], position, end - position, StringComparison.Ordinal);
                if (found < 0) return false;
                position = found + _parts[i].Length;
            }
            return true;
        }
    }
}
