using Microsoft.Extensions.Logging.Abstractions;
using SlopChat.Configuration;
using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{
  public class ConversationManagerImageStrippingTests
  {
    private static ConversationManager CreateManager(OpenRouterClient? client = null)
    {
      client ??= new NeverCalledOpenRouterClient();
      return new ConversationManager(
        client,
        new BotOptions(),
        NullLogger<ConversationManager>.Instance
      );
    }

    [Fact]
    public void AddAssistantMessage_NoMultimodalMessages_DoesNothing()
    {
      var manager = CreateManager();
      manager.AddUserMessage(1, "plain text");

      manager.AddAssistantMessage(1, "reply");

      var snapshot = manager.GetSnapshot(1);
      var userMsg = snapshot.First(m => m.Role == "user");
      Assert.Equal("plain text", userMsg.Content as string);
    }

    [Fact]
    public void AddAssistantMessage_SingleMultimodalUserMessage_KeepsItsImages()
    {
      var manager = CreateManager();
      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("describe this"),
        ContentPart.Image("data:image/png;base64,abc")
      ]));

      manager.AddAssistantMessage(1, "A nice picture");

      var snapshot = manager.GetSnapshot(1);
      var userMsg = snapshot.First(m => m.Role == "user");
      Assert.IsType<List<ContentPart>>(userMsg.Content);
    }

    [Fact]
    public void AddAssistantMessage_TwoMultimodalUserMessages_StripsFirstKeepsLast()
    {
      var manager = CreateManager();
      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("first text"),
        ContentPart.Image("data:image/png;base64,img1")
      ]));
      manager.AddAssistantMessage(1, "reply 1");

      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("second text"),
        ContentPart.Image("data:image/png;base64,img2")
      ]));
      manager.AddAssistantMessage(1, "reply 2");

      var snapshot = manager.GetSnapshot(1);
      var userMessages = snapshot.Where(m => m.Role == "user").ToList();
      Assert.Equal(2, userMessages.Count);

      Assert.Equal("first text", userMessages[0].Content as string);
      Assert.IsType<List<ContentPart>>(userMessages[1].Content);
    }

    [Fact]
    public void AddAssistantMessage_MultimodalUserWithNoText_UsesImagePlaceholder()
    {
      var manager = CreateManager();
      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.Image("data:image/png;base64,img1")
      ]));
      manager.AddAssistantMessage(1, "reply 1");

      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("second"),
        ContentPart.Image("data:image/png;base64,img2")
      ]));
      manager.AddAssistantMessage(1, "reply 2");

      var snapshot = manager.GetSnapshot(1);
      var userMessages = snapshot.Where(m => m.Role == "user").ToList();
      Assert.Equal("[image]", userMessages[0].Content as string);
    }

    [Fact]
    public void AddMessage_AssistantRole_TriggersStripping()
    {
      var manager = CreateManager();
      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("first"),
        ContentPart.Image("data:image/png;base64,img1")
      ]));
      manager.AddMessage(1, ChatMessage.Assistant("reply 1"));

      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("second"),
        ContentPart.Image("data:image/png;base64,img2")
      ]));
      manager.AddMessage(1, ChatMessage.Assistant("reply 2"));

      var snapshot = manager.GetSnapshot(1);
      var userMessages = snapshot.Where(m => m.Role == "user").ToList();
      Assert.Equal("first", userMessages[0].Content as string);
      Assert.IsType<List<ContentPart>>(userMessages[1].Content);
    }

    [Fact]
    public void AddAssistantMessage_PlainTextTurnAfterMultimodal_StripsEarlierImageParts()
    {
      var manager = CreateManager();
      manager.AddMessage(1, ChatMessage.UserMultimodal([
        ContentPart.TextContent("describe this photo"),
        ContentPart.Image("data:image/png;base64,img1")
      ]));
      manager.AddAssistantMessage(1, "Here is a description");

      manager.AddUserMessage(1, "tell me more");
      manager.AddAssistantMessage(1, "More details");

      var snapshot = manager.GetSnapshot(1);
      var userMessages = snapshot.Where(m => m.Role == "user").ToList();
      Assert.Equal(2, userMessages.Count);

      Assert.Equal("describe this photo", userMessages[0].Content as string);
      Assert.Equal("tell me more", userMessages[1].Content as string);
    }

    private sealed class NeverCalledOpenRouterClient : OpenRouterClient
    {
      public NeverCalledOpenRouterClient()
        : base(new HttpClient(), "test-key", NullLogger<OpenRouterClient>.Instance)
      {
      }

      public override Task<string> GetCompletionAsync(
        List<ChatMessage> messages,
        string model,
        CancellationToken ct,
        IToolExecutor? toolExecutor = null
      ) => throw new InvalidOperationException("Should not be called in this test");
    }
  }
}
