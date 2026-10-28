using System.Globalization;

namespace Glyphlog;

/// <summary>
/// Fixed phrases of <see cref="StartupBanner"/>. Ships <see cref="English"/> and <see cref="Portuguese"/>; any other
/// language is a <c>new BannerText(...)</c>.
/// </summary>
/// <param name="Ready">Follows the app name on the first line, after a dash.</param>
/// <param name="StaticFiles">Precedes the web root folder, when the app serves one.</param>
/// <param name="Environment">Precedes the environment name.</param>
/// <param name="Stop">How to stop the app.</param>
public sealed record BannerText(string Ready, string StaticFiles, string Environment, string Stop) {
    /// <summary>English phrases.</summary>
    public static BannerText English { get; } = new("ready", "Serving static files from", "Environment", "Ctrl+C to shut down");

    /// <summary>Portuguese phrases.</summary>
    public static BannerText Portuguese { get; } = new("pronta", "Servindo arquivos estáticos de", "Ambiente", "Ctrl+C para encerrar");

    /// <summary><see cref="Portuguese"/> when the current UI culture is Portuguese; <see cref="English"/> otherwise.</summary>
    public static BannerText ForCurrentCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt" ? Portuguese : English;
}
