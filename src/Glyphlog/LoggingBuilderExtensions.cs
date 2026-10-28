using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Text;

namespace Glyphlog;

/// <summary>Registers <see cref="GlyphlogFormatter"/> on the console logger.</summary>
public static class LoggingBuilderExtensions {
    /// <summary>
    /// Replaces the console's default formatter with <see cref="GlyphlogFormatter"/> — one line per log, an emoji per
    /// subject. Switching the console to UTF-8 comes along on purpose: without it the Windows console eats the emoji,
    /// and it is exactly the detail a registration copied from one <c>Program.cs</c> to another would forget.
    /// </summary>
    /// <param name="logging">The logging builder.</param>
    /// <param name="configure">Maps the project's categories (<see cref="GlyphlogOptions.Map"/>), sets the timestamp…
    /// Runs after the <c>Logging:Console:FormatterOptions</c> section is bound, so code wins over configuration.</param>
    /// <returns>The same builder, to chain calls.</returns>
    public static ILoggingBuilder AddGlyphlog(this ILoggingBuilder logging, Action<GlyphlogOptions>? configure = null) {
        try {
            Console.OutputEncoding = Encoding.UTF8;   // without it the Windows console eats the emoji
        } catch (IOException) { /* no console attached (a service): keep the encoding as is */ }
        logging.AddConsoleFormatter<GlyphlogFormatter, GlyphlogOptions>();
        if (configure is not null) logging.Services.Configure(configure);
        logging.AddConsole(options => options.FormatterName = GlyphlogFormatter.FormatterName);
        return logging;
    }
}
