using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class VideoGenerationPollResponse
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("unsigned_urls")]
    public List<string>? UnsignedUrls { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("usage")]
    public VideoUsage? Usage { get; set; }
  }

  public class VideoUsage
  {
    [JsonPropertyName("cost")]
    public double? Cost { get; set; }
  }
}
