using System.Text.Json.Serialization;

namespace SlopChat.Models;

public class ImageGenerationRequest
{
  [JsonPropertyName("model")]
  public string Model { get; set; } = string.Empty;

  [JsonPropertyName("prompt")]
  public string Prompt { get; set; } = string.Empty;

  [JsonPropertyName("n")]
  public int N { get; set; } = 1;

  [JsonPropertyName("size")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? Size { get; set; }
}
