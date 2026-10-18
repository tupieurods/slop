using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ReasoningOptions
  {
    [JsonPropertyName("enabled")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Enabled { get; set; }
  }
}
