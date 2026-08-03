using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{

public class OpenRouterClientResolveFinalTextTests
{
  private static readonly string[] NoTools = [];

  [Fact]
  public void NonEmptyContent_NoToolCalls_ReturnsContent()
  {
    var message = new ChatChoiceMessage { Content = "Hello world" };

    string result = OpenRouterClient.ResolveFinalText(message, NoTools);

    Assert.Equal("Hello world", result);
  }

  [Fact]
  public void NonEmptyContent_WithUnknownToolCalls_ReturnsWrenchFallbackPrefixAndContent()
  {
    var message = new ChatChoiceMessage { Content = "Hello world" };

    string result = OpenRouterClient.ResolveFinalText(message, ["mystery_tool", "mystery_tool", "mystery_tool"]);

    Assert.Equal("🔧🔧🔧 Hello world", result);
  }

  [Fact]
  public void EmptyContent_NonEmptyReasoning_NoToolCalls_ReturnsReasoningWithEmoji()
  {
    var message = new ChatChoiceMessage { Content = "", Reasoning = "My thinking" };

    string result = OpenRouterClient.ResolveFinalText(message, NoTools);

    Assert.Equal("💭 My thinking", result);
  }

  [Fact]
  public void EmptyContent_NonEmptyReasoning_WithToolCalls_ReturnsIconPrefixAndReasoning()
  {
    var message = new ChatChoiceMessage { Content = null, Reasoning = "My thinking" };

    string result = OpenRouterClient.ResolveFinalText(message, ["web_search", "web_search"]);

    Assert.Equal("🌐🌐 💭 My thinking", result);
  }

  [Fact]
  public void EmptyContent_EmptyReasoning_NoToolCalls_ReturnsPlaceholder()
  {
    var message = new ChatChoiceMessage { Content = null, Reasoning = null };

    string result = OpenRouterClient.ResolveFinalText(message, NoTools);

    Assert.Equal("(no response)", result);
  }

  [Fact]
  public void EmptyContent_EmptyReasoning_WithToolCalls_ReturnsIconPrefixAndPlaceholder()
  {
    var message = new ChatChoiceMessage { Content = "", Reasoning = "" };

    string result = OpenRouterClient.ResolveFinalText(
      message,
      ["get_current_date", "web_search", "image_search", "web_search", "mystery_tool"]);

    Assert.Equal("📅🌐🖼️🌐🔧 (no response)", result);
  }

  [Fact]
  public void NullMessage_NoToolCalls_ReturnsPlaceholder()
  {
    string result = OpenRouterClient.ResolveFinalText(null, NoTools);

    Assert.Equal("(no response)", result);
  }

  [Fact]
  public void NullMessage_WithToolCalls_ReturnsIconPrefixAndPlaceholder()
  {
    string result = OpenRouterClient.ResolveFinalText(null, ["get_current_date"]);

    Assert.Equal("📅 (no response)", result);
  }

  [Fact]
  public void GetCurrentDate_SingleCall_UsesCalendarIcon()
  {
    var message = new ChatChoiceMessage { Content = "Today is..." };

    string result = OpenRouterClient.ResolveFinalText(message, ["get_current_date"]);

    Assert.Equal("📅 Today is...", result);
  }

  [Fact]
  public void WebSearch_SingleCall_UsesGlobeIcon()
  {
    var message = new ChatChoiceMessage { Content = "Found this online" };

    string result = OpenRouterClient.ResolveFinalText(message, ["web_search"]);

    Assert.Equal("🌐 Found this online", result);
  }

  [Fact]
  public void ImageSearch_SingleCall_UsesImageIcon()
  {
    var message = new ChatChoiceMessage { Content = "Here is a picture" };

    string result = OpenRouterClient.ResolveFinalText(message, ["image_search"]);

    Assert.Equal("🖼️ Here is a picture", result);
  }

  [Fact]
  public void MixedTools_PreservesOrder()
  {
    var message = new ChatChoiceMessage { Content = "Done" };

    string result = OpenRouterClient.ResolveFinalText(
      message,
      ["web_search", "image_search", "get_current_date"]);

    Assert.Equal("🌐🖼️📅 Done", result);
  }
}

}
