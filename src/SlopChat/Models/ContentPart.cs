using System.Text.Json.Serialization;

namespace SlopChat.Models;

public class ContentPart
{
  [JsonPropertyName("type")]
  public string Type { get; private init; } = string.Empty;

  [JsonPropertyName("text")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public string? Text { get; private init; }

  [JsonPropertyName("image_url")]
  [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
  public ImageUrlContent? ImageUrl { get; private init; }

  public static ContentPart TextContent(string text) => new() { Type = "text", Text = text };

  public static ContentPart Image(string dataUrl) => new()
  {
    Type = "image_url",
    ImageUrl = new ImageUrlContent { Url = dataUrl }
  };
}

public class ImageUrlContent
{
  [JsonPropertyName("url")]
  public string Url { get; init; } = string.Empty;
}
