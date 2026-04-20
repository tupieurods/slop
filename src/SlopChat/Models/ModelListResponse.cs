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

    [JsonPropertyName("output_modalities")]
    public List<string>? OutputModalities { get; set; }

    public bool IsImageGeneration =>
      OutputModalities?.Contains("image", StringComparer.OrdinalIgnoreCase) == true;
  }
}
