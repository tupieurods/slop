using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class ImagesApiResponse
  {
    [JsonPropertyName("data")]
    public List<ImagesApiData> Data { get; init; } = [];

    [JsonPropertyName("usage")]
    public CompletionUsage? Usage { get; init; }
  }
}
