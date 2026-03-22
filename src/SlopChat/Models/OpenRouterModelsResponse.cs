using System.Text.Json.Serialization;

namespace SlopChat.Models
{
    public class OpenRouterModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenRouterModel> Data { get; set; } = new();
    }

    public class OpenRouterModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }
}
