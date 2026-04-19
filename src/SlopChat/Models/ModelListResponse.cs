using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ModelListResponse
  {
    [JsonPropertyName("data")]
    public List<ModelInfo> Data { get; set; } = [];
  }

  public class ModelInfo
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
  }
}
