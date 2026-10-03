namespace SlopMcp.Services.Visualization {

  public sealed record WorldMapResult
  {
    public required byte[] Png { get; init; }
    public IReadOnlyList<string> UnresolvedCountries { get; init; } = [];
  }

}
