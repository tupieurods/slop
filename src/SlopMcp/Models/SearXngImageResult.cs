namespace SlopMcp.Models {

  public class SearXngImageResult
  {
    public required string Title { get; init; }
    public required string ImageUrl { get; init; }
    public required string SourceUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? Resolution { get; init; }
  }

}
