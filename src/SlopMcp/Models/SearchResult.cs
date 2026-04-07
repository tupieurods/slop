namespace SlopMcp.Models {

  public class SearchResult
  {
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string Content { get; init; } = string.Empty;
  }

}
