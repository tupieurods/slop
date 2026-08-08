namespace SlopMcp.Configuration {

  public class Crawl4AiOptions
  {
    public string BaseUrl { get; init; } = "http://crawl4ai:11235";
    public string Token { get; init; } = string.Empty;
    public string CallbackUrl { get; init; } = "http://slopmcp:8080/internal/crawl4ai-callback";
    public int TimeoutSeconds { get; init; } = 90;
  }

}
