using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class VideoModelInfo
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
  }
}
