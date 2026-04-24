using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class VideoGenerationSubmitResponse
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("polling_url")]
    public string? PollingUrl { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
  }
}
