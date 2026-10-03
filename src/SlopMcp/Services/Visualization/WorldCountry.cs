using ScottPlot;

namespace SlopMcp.Services.Visualization {

  public sealed record WorldCountry
  {
    public required string Name { get; init; }
    public string? Iso2 { get; init; }
    public string? Iso3 { get; init; }
    public IReadOnlyList<string> Codes { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public required IReadOnlyList<Coordinates[]> Polygons { get; init; }
    public double Area { get; init; }
  }

}
