using System.Text.Json;
using System.Text.Json.Serialization;
using SlopChat.Models;

namespace SlopChat.Tests
{
  public class ChatCompletionRequestSerializationTests
  {
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void MaxTokens_WhenSet_SerializesInJson()
    {
      var request = new ChatCompletionRequest
      {
        Model = "test-model",
        Messages = [],
        MaxTokens = 4096
      };

      string json = JsonSerializer.Serialize(request, JsonOptions);

      Assert.Contains("\"max_tokens\"", json);
      Assert.Contains("4096", json);
    }

    [Fact]
    public void MaxTokens_WhenNull_OmittedFromJson()
    {
      var request = new ChatCompletionRequest
      {
        Model = "test-model",
        Messages = []
      };

      string json = JsonSerializer.Serialize(request, JsonOptions);

      Assert.DoesNotContain("max_tokens", json);
    }
  }
}
