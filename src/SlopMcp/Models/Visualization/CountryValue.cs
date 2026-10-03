using System.ComponentModel;

namespace SlopMcp.Models.Visualization {

  public sealed record CountryValue
  {
    [Description("Country as ISO 3166-1 alpha-2 code (US), alpha-3 code (USA) or English name (United States).")]
    public required string Country { get; init; }

    [Description("Numeric statistic for the country; mapped to the colormap.")]
    public double? Value { get; init; }

    [Description("Optional explicit fill color as hex (e.g. #E41A1C); overrides the colormap. Use for categorical maps.")]
    public string? Color { get; init; }
  }

}
