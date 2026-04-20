using System.Text.Json.Serialization;

namespace SlopChat.Models;

public class ImageGenerationResponse
{
  [JsonPropertyName("data")]
  public List<ImageData> Data { get; set; } = [];
}

public class ImageData
{
  [JsonPropertyName("b64_json")]
  public string? B64Json { get; set; }

  [JsonPropertyName("url")]
  public string? Url { get; set; }
}
