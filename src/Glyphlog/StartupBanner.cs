using Microsoft.AspNetCore.Builder;

namespace Glyphlog;

/// <summary>
/// The app's identity card on the console, printed <b>after</b> the server starts — only then does
/// <see cref="WebApplication.Urls"/> hold the ports Kestrel actually bound (with port 0 or dynamic ports, the list is
/// empty before that). Call it after <c>await app.StartAsync()</c>, or from <c>app.Lifetime.ApplicationStarted</c>.
/// <para>
/// It stands in for the four <c>Microsoft.Hosting.Lifetime</c> logs ("Now listening on…", "Application started",
/// "Hosting environment", "Content root path"): set that category to <c>Warning</c>, or the same information comes
/// out twice.
/// </para>
/// <para>
/// The skeleton is the same in every app (name, addresses, docs, static files, environment, Ctrl+C); what belongs to
/// one app only — its database, a downstream service — comes in through <c>lines</c>, in the given order, right after
/// the docs.
/// </para>
/// <para>Writes straight to the <see cref="Console"/>, not through <c>ILogger</c>: the banner must not depend on log levels.</para>
/// </summary>
public static class StartupBanner {
    private const string Reset = "\x1b[0m";
    private const string Bold = "\x1b[1m";
    private const string Dim = "\x1b[90m";
    private const string Blue = "\x1b[94m";

    // Only emoji with Emoji_Presentation=Yes (no U+FE0F variation selector): terminals always draw those in 2 cells,
    // so the text of every line starts on the same column. With FE0F (⚠️ 🗄️ ℹ️) the width drops to 1 cell and the
    // line shifts. Applies to the icons callers pass in `lines` too.
    private const string IconApp = "🚀";
    private const string IconUrl = "🔗";
    private const string IconDocs = "📗";
    private const string IconStatic = "📦";
    private const string IconEnvironment = "🧭";
    private const string IconStop = "🛑";

    /// <summary>Writes the banner with the phrases of the current UI culture (<see cref="BannerText.ForCurrentCulture"/>).</summary>
    /// <param name="app">Already started — see the summary.</param>
    /// <param name="name">Name in bold on the first line (e.g. <c>Orders API</c>).</param>
    /// <param name="docs">Documentation routes, on one line, separated by <c>·</c>. Empty omits the line.</param>
    /// <param name="lines">The app's own lines. A null or blank text omits the line: that is how optional information
    /// (a database without a connection string) disappears without an <c>if</c> at the call site.</param>
    public static void Write(WebApplication app, string name, IReadOnlyList<string> docs, params (string Icon, string? Text)[] lines) =>
        Write(app, BannerText.ForCurrentCulture(), name, docs, lines);

    /// <summary>Writes the banner with the given fixed phrases.</summary>
    /// <param name="app">Already started — see the summary.</param>
    /// <param name="text">Fixed phrases: <see cref="BannerText.English"/>, <see cref="BannerText.Portuguese"/> or your own.</param>
    /// <param name="name">Name in bold on the first line (e.g. <c>Orders API</c>).</param>
    /// <param name="docs">Documentation routes, on one line, separated by <c>·</c>. Empty omits the line.</param>
    /// <param name="lines">The app's own lines. A null or blank text omits the line.</param>
    public static void Write(WebApplication app, BannerText text, string name, IReadOnlyList<string> docs, params (string Icon, string? Text)[] lines) {
        bool color = !Console.IsOutputRedirected;
        TextWriter output = Console.Out;

        output.WriteLine();
        Line(output, IconApp, Emphasis(color, name) + Faint(color, " — " + text.Ready));

        foreach (string url in Addresses(app)) Line(output, IconUrl, Link(color, url));

        if (docs.Count > 0) Line(output, IconDocs, string.Join(Faint(color, "  ·  "), docs));

        foreach ((string icon, string? line) in lines) {
            if (!string.IsNullOrWhiteSpace(line)) Line(output, icon, line);
        }

        string? webRoot = app.Environment.WebRootPath;
        if (!string.IsNullOrEmpty(webRoot) && Directory.Exists(webRoot)) {
            Line(output, IconStatic, $"{text.StaticFiles} /{Path.GetFileName(Path.TrimEndingDirectorySeparator(webRoot))}");
        }

        Line(output, IconEnvironment, $"{text.Environment} {app.Environment.EnvironmentName}");
        Line(output, IconStop, Faint(color, text.Stop));
        output.WriteLine();
        output.Flush();
    }

    private static void Line(TextWriter output, string icon, string text) {
        output.Write(icon);
        output.Write("  ");
        output.WriteLine(text);
    }

    /// <summary>Bound addresses; falls back to <c>ASPNETCORE_URLS</c> when the server does not expose the list.</summary>
    private static IEnumerable<string> Addresses(WebApplication app) {
        if (app.Urls.Count > 0) return app.Urls.OrderBy(x => x, StringComparer.Ordinal);
        string? configured = app.Configuration["ASPNETCORE_URLS"] ?? app.Configuration["urls"];
        return string.IsNullOrWhiteSpace(configured) ? [] : configured.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Emphasis(bool color, string text) => color ? Bold + text + Reset : text;
    private static string Faint(bool color, string text) => color ? Dim + text + Reset : text;
    private static string Link(bool color, string text) => color ? Blue + text + Reset : text;
}
