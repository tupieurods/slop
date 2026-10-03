using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopChat.Tests
{
  public class WorldMapRendererTests
  {
    private static readonly WorldMapRenderer Renderer = new();

    [Theory]
    [InlineData("US", "USA")]
    [InlineData("USA", "USA")]
    [InlineData("united states", "USA")]
    [InlineData("United States of America", "USA")]
    [InlineData("FR", "FRA")]
    [InlineData("France", "FRA")]
    [InlineData("NOR", "NOR")]
    [InlineData("no", "NOR")]
    [InlineData("Россия", "RUS")]
    [InlineData("UK", "GBR")]
    [InlineData("Ivory Coast", "CIV")]
    [InlineData("Kosovo", "KOS")]
    [InlineData("РФ", "RUS")]
    public void TryResolveCountry_KnownInput_ResolvesToIso3(string input, string expectedIso3)
    {
      bool resolved = Renderer.TryResolveCountry(input, out WorldCountry? country);

      Assert.True(resolved);
      Assert.NotNull(country);
      Assert.Equal(expectedIso3, country.Iso3);
    }

    [Theory]
    [InlineData("Atlantis")]
    [InlineData("")]
    [InlineData("XX")]
    public void TryResolveCountry_UnknownInput_ReturnsFalse(string input)
    {
      Assert.False(Renderer.TryResolveCountry(input, out _));
    }

    [Fact]
    public void Render_NumericValues_ReturnsPngAndReportsUnresolved()
    {
      CountryValue[] values =
      [
        new CountryValue { Country = "USA", Value = 335 },
        new CountryValue { Country = "China", Value = 1410 },
        new CountryValue { Country = "IN", Value = 1430 },
        new CountryValue { Country = "Atlantis", Value = 1 }
      ];

      WorldMapResult result = Renderer.Render(values, "Population", "millions");

      Assert.True(PngAssert.IsPng(result.Png));
      Assert.Equal(["Atlantis"], result.UnresolvedCountries);
    }

    [Fact]
    public void Render_ExplicitColors_ReturnsPng()
    {
      CountryValue[] values =
      [
        new CountryValue { Country = "DE", Color = "#E41A1C" },
        new CountryValue { Country = "PL", Color = "#377EB8" }
      ];

      WorldMapResult result = Renderer.Render(values);

      Assert.True(PngAssert.IsPng(result.Png));
      Assert.Empty(result.UnresolvedCountries);
    }

    [Fact]
    public void Render_SingleCountryNumeric_ReturnsPng()
    {
      WorldMapResult result = Renderer.Render([new CountryValue { Country = "BR", Value = 216 }], "Brazil");

      Assert.True(PngAssert.IsPng(result.Png));
    }

    [Fact]
    public void Render_AllValuesEqual_ReturnsPng()
    {
      CountryValue[] values = [new CountryValue { Country = "BR", Value = 0 }, new CountryValue { Country = "AR", Value = 0 }];

      Assert.True(PngAssert.IsPng(Renderer.Render(values).Png));
    }

    [Fact]
    public void Render_NothingResolved_Throws()
    {
      Assert.Throws<ArgumentException>(() => Renderer.Render([new CountryValue { Country = "Atlantis", Value = 1 }]));
    }

    [Fact]
    public void Render_UnknownColormap_Throws()
    {
      Assert.Throws<ArgumentException>(() => Renderer.Render([new CountryValue { Country = "US", Value = 1 }], colormap: "rainbow-unicorn"));
    }

    [Fact]
    public void Render_InvalidColor_Throws()
    {
      Assert.Throws<ArgumentException>(() => Renderer.Render([new CountryValue { Country = "US", Color = "red-ish" }]));
    }
  }
}
