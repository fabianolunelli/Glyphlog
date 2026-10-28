using System.Globalization;

namespace Glyphlog.Tests;

public class BannerTextTests {
    [Theory]
    [InlineData("pt-BR", "pronta")]
    [InlineData("pt-PT", "pronta")]
    [InlineData("en-US", "ready")]
    [InlineData("de-DE", "ready")]
    public void Follows_the_ui_culture(string culture, string ready) {
        CultureInfo previous = CultureInfo.CurrentUICulture;
        try {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            Assert.Equal(ready, BannerText.ForCurrentCulture().Ready);
        } finally {
            CultureInfo.CurrentUICulture = previous;
        }
    }
}
