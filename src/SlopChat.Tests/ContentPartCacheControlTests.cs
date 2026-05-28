using System.Text.Json;
using System.Text.Json.Serialization;
using SlopChat.Models;

namespace SlopChat.Tests
{
  public class ContentPartCacheControlTests
  {
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void TextContent_WithEphemeral_SerializesCacheControl()
    {
      ContentPart part = ContentPart.TextContent("Hello", ephemeral: true);

      string json = JsonSerializer.Serialize(part, JsonOptions);

      Assert.Contains("\"cache_control\"", json);
      Assert.Contains("\"ephemeral\"", json);
    }

    [Fact]
    public void TextContent_WithoutEphemeral_OmitsCacheControl()
    {
      ContentPart part = ContentPart.TextContent("Hello");

      string json = JsonSerializer.Serialize(part, JsonOptions);

      Assert.DoesNotContain("cache_control", json);
    }

    [Fact]
    public void ImageContent_OmitsCacheControl()
    {
      ContentPart part = ContentPart.Image("data:image/png;base64,abc");

      string json = JsonSerializer.Serialize(part, JsonOptions);

      Assert.DoesNotContain("cache_control", json);
    }

    [Fact]
    public void SystemMessage_ContentIsListWithCacheControl()
    {
      ChatMessage msg = ChatMessage.System("System prompt text", ephemeral: true);

      Assert.IsType<List<ContentPart>>(msg.Content);
      var parts = (List<ContentPart>)msg.Content!;
      Assert.Single(parts);
      Assert.Equal("text", parts[0].Type);
      Assert.Equal("System prompt text", parts[0].Text);
      Assert.NotNull(parts[0].CacheControl);
      Assert.Equal("ephemeral", parts[0].CacheControl!.Type);
    }

    [Fact]
    public void SystemMessage_SerializesWithCacheControlOnContentPart()
    {
      ChatMessage msg = ChatMessage.System("Test system", ephemeral: true);

      string json = JsonSerializer.Serialize(msg, JsonOptions);

      Assert.Contains("\"cache_control\"", json);
      Assert.Contains("\"ephemeral\"", json);
    }

    [Fact]
    public void SystemMessage_PlainString_SerializesContentAsString_NoCacheControl()
    {
      ChatMessage msg = ChatMessage.System("plain system");

      string json = JsonSerializer.Serialize(msg, JsonOptions);

      Assert.IsType<string>(msg.Content);
      Assert.DoesNotContain("cache_control", json);
    }
  }
}
