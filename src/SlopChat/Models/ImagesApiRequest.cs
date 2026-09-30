using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ImagesApiRequest
  {
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; init; } = string.Empty;

    [JsonPropertyName("input_references")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ContentPart>? InputReferences { get; init; }
  }
}
