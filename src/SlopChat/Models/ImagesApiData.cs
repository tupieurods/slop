using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ImagesApiData
  {
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; init; }

    [JsonPropertyName("media_type")]
    public string? MediaType { get; init; }
  }
}
