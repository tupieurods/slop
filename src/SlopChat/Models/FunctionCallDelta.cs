using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class FunctionCallDelta
  {
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
  }
}
