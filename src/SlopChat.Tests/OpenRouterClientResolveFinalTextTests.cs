using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{

public class OpenRouterClientResolveFinalTextTests
{
  [Fact]
  public void NonEmptyContent_NoToolCalls_ReturnsContent()
  {
    var message = new ChatChoiceMessage { Content = "Hello world" };

    string result = OpenRouterClient.ResolveFinalText(message, 0);

    Assert.Equal("Hello world", result);
  }

  [Fact]
  public void NonEmptyContent_WithToolCalls_ReturnsWrenchPrefixAndContent()
  {
    var message = new ChatChoiceMessage { Content = "Hello world" };

    string result = OpenRouterClient.ResolveFinalText(message, 3);

    Assert.Equal("🔧🔧🔧 Hello world", result);
  }

  [Fact]
  public void EmptyContent_NonEmptyReasoning_NoToolCalls_ReturnsReasoningWithEmoji()
  {
    var message = new ChatChoiceMessage { Content = "", Reasoning = "My thinking" };

    string result = OpenRouterClient.ResolveFinalText(message, 0);

    Assert.Equal("💭 My thinking", result);
  }

  [Fact]
  public void EmptyContent_NonEmptyReasoning_WithToolCalls_ReturnsWrenchPrefixAndReasoning()
  {
    var message = new ChatChoiceMessage { Content = null, Reasoning = "My thinking" };

    string result = OpenRouterClient.ResolveFinalText(message, 2);

    Assert.Equal("🔧🔧 💭 My thinking", result);
  }

  [Fact]
  public void EmptyContent_EmptyReasoning_NoToolCalls_ReturnsPlaceholder()
  {
    var message = new ChatChoiceMessage { Content = null, Reasoning = null };

    string result = OpenRouterClient.ResolveFinalText(message, 0);

    Assert.Equal("(no response)", result);
  }

  [Fact]
  public void EmptyContent_EmptyReasoning_WithToolCalls_ReturnsWrenchPrefixAndPlaceholder()
  {
    var message = new ChatChoiceMessage { Content = "", Reasoning = "" };

    string result = OpenRouterClient.ResolveFinalText(message, 5);

    Assert.Equal("🔧🔧🔧🔧🔧 (no response)", result);
  }

  [Fact]
  public void NullMessage_NoToolCalls_ReturnsPlaceholder()
  {
    string result = OpenRouterClient.ResolveFinalText(null, 0);

    Assert.Equal("(no response)", result);
  }

  [Fact]
  public void NullMessage_WithToolCalls_ReturnsWrenchPrefixAndPlaceholder()
  {
    string result = OpenRouterClient.ResolveFinalText(null, 1);

    Assert.Equal("🔧 (no response)", result);
  }
}

}
