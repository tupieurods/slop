using Microsoft.Extensions.Logging.Abstractions;
using SlopChat.Configuration;
using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{
  public class ConversationManagerSummaryModelFallbackTests
  {
    [Fact]
    public async Task CompactIfNeededAsync_SummaryModelFails_FallsBackToChatModel()
    {
      const string summaryModelId = "google/gemini-2.5-flash-lite";
      const string chatModelId = "some/chat-model";
      const string expectedSummary = "Summary text";

      var fakeClient = new ControlledCompletionClient(summaryModelId, expectedSummary);
      var manager = new ConversationManager(
        fakeClient,
        new BotOptions(),
        NullLogger<ConversationManager>.Instance
      );

      manager.SetModel(1, chatModelId);

      for(int i = 0; i < 62; i++)
      {
        manager.AddUserMessage(1, $"User message {i}");
        manager.AddAssistantMessage(1, $"Assistant reply {i}");
      }

      await manager.CompactIfNeededAsync(1, CancellationToken.None);

      Assert.Contains(chatModelId, fakeClient.CalledModels);
    }

    [Fact]
    public async Task CompactIfNeededAsync_SummaryModelSucceeds_DoesNotCallChatModel()
    {
      const string chatModelId = "some/chat-model";
      const string expectedSummary = "Summary text";

      var fakeClient = new ControlledCompletionClient(failModel: null, expectedSummary);
      var manager = new ConversationManager(
        fakeClient,
        new BotOptions(),
        NullLogger<ConversationManager>.Instance
      );

      manager.SetModel(1, chatModelId);

      for(int i = 0; i < 62; i++)
      {
        manager.AddUserMessage(1, $"User message {i}");
        manager.AddAssistantMessage(1, $"Assistant reply {i}");
      }

      await manager.CompactIfNeededAsync(1, CancellationToken.None);

      Assert.DoesNotContain(chatModelId, fakeClient.CalledModels);
    }

    private sealed class ControlledCompletionClient : OpenRouterClient
    {
      private readonly string? _failModel;
      private readonly string _successResponse;

      public List<string> CalledModels { get; } = [];

      public ControlledCompletionClient(string? failModel, string successResponse)
        : base(new HttpClient(), "test-key", NullLogger<OpenRouterClient>.Instance)
      {
        _failModel = failModel;
        _successResponse = successResponse;
      }

      public override Task<string> GetCompletionAsync(
        List<ChatMessage> messages,
        string model,
        CancellationToken ct,
        IToolExecutor? toolExecutor = null
      )
      {
        if(model == _failModel)
        {
          throw new HttpRequestException("Simulated model failure");
        }

        CalledModels.Add(model);
        return Task.FromResult(_successResponse);
      }
    }
  }
}
