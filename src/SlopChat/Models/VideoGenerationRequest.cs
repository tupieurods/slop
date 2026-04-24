using System.Text.Json.Serialization;

namespace SlopChat.Models
{
  public class VideoGenerationRequest
  {
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("frame_images")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FrameImage>? FrameImages { get; set; }
  }

  public class FrameImage
  {
    [JsonPropertyName("type")]
    public string Type { get; set; } = "image_url";

    [JsonPropertyName("image_url")]
    public FrameImageUrl ImageUrl { get; set; } = new();

    [JsonPropertyName("frame_type")]
    public string FrameType { get; set; } = "first_frame";
  }

  public class FrameImageUrl
  {
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
  }
}
