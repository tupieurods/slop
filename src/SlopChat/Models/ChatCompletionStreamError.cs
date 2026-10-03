using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ChatCompletionStreamError
  {
    [JsonPropertyName("code")]
    public JsonElement? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
  }
}
