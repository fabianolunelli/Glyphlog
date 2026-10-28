using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Glyphlog.Tests;

public class GlyphlogFormatterTests {
    [Fact]
    public void Information_takes_the_category_icon() =>
        Assert.Equal("💾  select 1\n", Format(LogLevel.Information, "Microsoft.EntityFrameworkCore.Database.Command", "select 1"));

    [Theory]
    [InlineData("Microsoft.AspNetCore.Authentication.Cookies", "🔑")]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware", "🌐")]
    [InlineData("System.Net.Http.HttpClient.Default.LogicalHandler", "🌐")]
    [InlineData("Microsoft.Hosting.Lifetime", "🚀")]
    [InlineData("MyApp.Billing", "💬")]
    public void Framework_categories_come_mapped(string category, string icon) =>
        Assert.Equal($"{icon}  x\n", Format(LogLevel.Information, category, "x"));

    [Fact]
    public void Prefix_matches_whole_segments_only() =>
        Assert.Equal("💬  x\n", Format(LogLevel.Information, "Microsoft.AspNetCoreX.Foo", "x"));

    [Fact]
    public void Map_adds_the_project_categories() {
        GlyphlogOptions options = new GlyphlogOptions().Map("🌌", "MyApp.Engine");
        Assert.Equal("🌌  tick\n", Format(LogLevel.Information, "MyApp.Engine.Simulation", "tick", options));
    }

    [Fact]
    public void The_longest_prefix_wins_whatever_the_order() {
        GlyphlogOptions options = new GlyphlogOptions().Map("🌌", "MyApp.Engine").Map("📦", "MyApp");
        Assert.Equal("🌌  x\n", Format(LogLevel.Information, "MyApp.Engine.Clock", "x", options));
        Assert.Equal("📦  x\n", Format(LogLevel.Information, "MyApp.Billing", "x", options));
    }

    [Theory]
    [InlineData("MyApp.Services.WeatherService", "⛅")]
    [InlineData("MyApp.Controllers.WeatherEventsController", "⛅")]
    [InlineData("MyApp.Services.Engine.Simulation", "🌌")]
    [InlineData("MyApp.Engine", "💬")]
    [InlineData("MyApp.Services.Clock", "💬")]
    public void Wildcard_matches_anywhere_in_the_category(string category, string icon) {
        GlyphlogOptions options = new GlyphlogOptions().Map("⛅", "*Weather*").Map("🌌", "*.Engine.*");
        Assert.Equal($"{icon}  x\n", Format(LogLevel.Information, category, "x", options));
    }

    [Theory]
    [InlineData("MyApp.Services.NpcBrain", "🧠")]
    [InlineData("MyApp.Services.Orders", "📦")]
    [InlineData("Other.NpcThing", "🤖")]
    public void The_most_literal_characters_win_between_prefix_and_wildcard(string category, string icon) {
        GlyphlogOptions options = new GlyphlogOptions().Map("🤖", "*Npc*").Map("📦", "MyApp").Map("🧠", "MyApp*Npc*");
        Assert.Equal($"{icon}  x\n", Format(LogLevel.Information, category, "x", options));
    }

    [Theory]
    [InlineData("⚠️")]
    [InlineData("🗄️")]
    [InlineData("ab")]
    [InlineData("")]
    public void Map_refuses_what_breaks_the_column(string icon) =>
        Assert.Throws<ArgumentException>(() => new GlyphlogOptions().Map(icon, "MyApp"));

    [Fact]
    public void Warning_shows_the_short_origin_and_the_event_id() =>
        Assert.Equal("🚧  Invoices[42] slow\n", Format(LogLevel.Warning, "MyApp.Billing.Invoices", "slow", eventId: new EventId(42)));

    [Fact]
    public void Error_without_event_id_shows_only_the_origin() =>
        Assert.Equal("❌  Invoices failed\n", Format(LogLevel.Error, "MyApp.Billing.Invoices", "failed"));

    [Fact]
    public void Debug_takes_the_magnifier() =>
        Assert.Equal("🔍  x\n", Format(LogLevel.Debug, "Microsoft.EntityFrameworkCore", "x"));

    [Fact]
    public void Multiline_message_becomes_an_indented_block() =>
        Assert.Equal("💬  first\n    second\n", Format(LogLevel.Information, "MyApp", "first\r\nsecond"));

    [Fact]
    public void Message_with_its_own_glyph_passes_untouched() =>
        Assert.Equal("🚀  Ready\n🌐  http://localhost\n", Format(LogLevel.Information, "MyApp", "🚀  Ready\n🌐  http://localhost"));

    [Fact]
    public void Message_starting_with_a_letter_of_any_script_gets_an_icon() =>
        Assert.Equal("💬  日本語\n", Format(LogLevel.Information, "MyApp", "日本語"));

    [Fact]
    public void Warning_with_its_own_glyph_keeps_the_level_icon() =>
        Assert.Equal("🚧  MyApp 🔥 hot\n", Format(LogLevel.Warning, "MyApp", "🔥 hot"));

    [Fact]
    public void Exception_goes_indented_below() =>
        Assert.StartsWith("❌  MyApp boom\n    System.InvalidOperationException: bad",
            Format(LogLevel.Error, "MyApp", "boom", exception: new InvalidOperationException("bad")));

    [Fact]
    public void Categories_bind_from_configuration() {
        var options = new GlyphlogOptions();
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Categories:MyApp.Billing"] = "💰",
                ["Categories:*Report*"] = "📊",
                ["DefaultIcon"] = "📝",
            })
            .Build()
            .Bind(options);

        Assert.Equal("💰  paid\n", Format(LogLevel.Information, "MyApp.Billing.Invoices", "paid", options));
        Assert.Equal("📊  ready\n", Format(LogLevel.Information, "MyApp.Sales.MonthlyReportJob", "ready", options));
        Assert.Equal("📝  other\n", Format(LogLevel.Information, "MyApp.Shipping", "other", options));
        Assert.Equal("💾  kept\n", Format(LogLevel.Information, "Microsoft.EntityFrameworkCore", "kept", options));
    }

    private static string Format(LogLevel level, string category, string message, GlyphlogOptions? options = null,
        EventId eventId = default, Exception? exception = null) {
        using var formatter = new GlyphlogFormatter(new FixedOptions(options ?? new GlyphlogOptions()), color: false);
        var writer = new StringWriter { NewLine = "\n" };
        formatter.Write(new LogEntry<string>(level, category, eventId, message, exception, static (state, _) => state), null, writer);
        return writer.ToString();
    }

    private sealed class FixedOptions(GlyphlogOptions value) : IOptionsMonitor<GlyphlogOptions> {
        public GlyphlogOptions CurrentValue => value;
        public GlyphlogOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<GlyphlogOptions, string?> listener) => null;
    }
}
