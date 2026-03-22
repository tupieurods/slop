using System.Text.Json.Serialization;

namespace SlopChat.Models
{
    public class OpenRouterRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();
    }
}
