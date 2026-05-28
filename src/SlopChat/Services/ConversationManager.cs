using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Models;

namespace SlopChat.Services
{
  public class ConversationManager
  {
    private readonly ConcurrentDictionary<long, List<ChatMessage>> _histories = new();
    private readonly ConcurrentDictionary<long, string> _models = new();
    private readonly ConcurrentDictionary<long, string> _drawModels = new();
    private readonly ConcurrentDictionary<long, string> _videoModels = new();
    private readonly ConcurrentDictionary<long, bool> _compacting = new();
    private readonly OpenRouterClient _openRouter;
    private readonly BotOptions _options;
    private readonly ILogger<ConversationManager> _logger;
    private readonly Lock _lock = new();
    private string _summaryModel = "google/gemini-2.5-flash-lite";
    private const int CompactThresholdPairs = 30;
    private const int KeepRecentPairs = 8;

    public ConversationManager(OpenRouterClient openRouter, BotOptions options, ILogger<ConversationManager> logger)
    {
      _openRouter = openRouter;
      _options = options;
      _logger = logger;
    }

    private List<ChatMessage> GetHistory(long chatId)
    {
      lock(_lock)
      {
        return _histories.GetOrAdd(chatId, _ => CreateInitialHistory());
      }
    }

    public void AddUserMessage(long chatId, string content)
    {
      lock(_lock)
      {
        GetHistory(chatId).Add(ChatMessage.User(content));
      }
    }

    public void AddMessage(long chatId, ChatMessage message)
    {
      lock(_lock)
      {
        var history = GetHistory(chatId);
        history.Add(message);
        if(message.Role == "assistant")
        {
          StripImagesFromEarlierMessages(history);
        }
      }
    }

    public void AddAssistantMessage(long chatId, string content)
    {
      lock(_lock)
      {
        var history = GetHistory(chatId);
        history.Add(ChatMessage.Assistant(content));
        StripImagesFromEarlierMessages(history);
      }
    }

    public List<ChatMessage> GetSnapshot(long chatId)
    {
      lock(_lock)
      {
        return [..GetHistory(chatId)];
      }
    }

    public void Reset(long chatId)
    {
      lock(_lock)
      {
        _histories[chatId] = CreateInitialHistory();
      }
    }

    public string GetModel(long chatId) => _models.GetOrAdd(chatId, _ => BotOptions.DefaultModel);

    public void SetModel(long chatId, string model)
    {
      _models[chatId] = model;
    }

    public string GetDrawModel(long chatId) => _drawModels.GetOrAdd(chatId, _ => BotOptions.DefaultDrawModel);

    public void SetDrawModel(long chatId, string model)
    {
      _drawModels[chatId] = model;
    }

    public string GetVideoModel(long chatId) => _videoModels.GetOrAdd(chatId, _ => BotOptions.DefaultVideoModel);

    public void SetVideoModel(long chatId, string model)
    {
      _videoModels[chatId] = model;
    }

    public string GetSummaryModel()
    {
      lock(_lock)
      {
        return _summaryModel;
      }
    }

    public void SetSummaryModel(string model)
    {
      lock(_lock)
      {
        _summaryModel = model;
      }
    }

    public async Task CompactIfNeededAsync(long chatId, CancellationToken ct)
    {
      int summarizeCount;
      List<ChatMessage> toSummarize;

      lock(_lock)
      {
        List<ChatMessage> history = GetHistory(chatId);
        int pairCount = (history.Count - 1) / 2;
        if(pairCount < CompactThresholdPairs)
        {
          return;
        }

        int keepMessages = KeepRecentPairs * 2;
        int summarizeEnd = history.Count - keepMessages;
        if(summarizeEnd <= 1)
        {
          return;
        }

        summarizeCount = summarizeEnd - 1;
        toSummarize = [..history[1..summarizeEnd]];
      }

      if(!_compacting.TryAdd(chatId, true))
      {
        return;
      }

      try
      {
        string summaryModel = GetSummaryModel();
        string chatModel = GetModel(chatId);
        List<ChatMessage> request = BuildSummarizationRequest(toSummarize);

        string summary;
        try
        {
          summary = await _openRouter.GetCompletionAsync(request, summaryModel, ct);
        }
        catch(Exception ex)
        {
          _logger.LogWarning(ex, "Summarization with model {SummaryModel} failed, retrying with {ChatModel}", summaryModel, chatModel);
          summary = await _openRouter.GetCompletionAsync(request, chatModel, ct);
        }

        lock(_lock)
        {
          List<ChatMessage> history = GetHistory(chatId);
          history.RemoveRange(1, summarizeCount);
          history.Insert(1, ChatMessage.Assistant($"Summary of previous conversation:\n{summary}"));
        }

        _logger.LogInformation("Compacted conversation history for chat {ChatId}", chatId);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to compact conversation history for chat {ChatId}", chatId);
      }
      finally
      {
        _compacting.TryRemove(chatId, out _);
      }
    }

    private List<ChatMessage> CreateInitialHistory() => [ChatMessage.System(_options.SystemPrompt, ephemeral: true)];

    private static void StripImagesFromEarlierMessages(List<ChatMessage> history)
    {
      int lastUserIdx = -1;
      for(int i = history.Count - 1; i >= 0; i--)
      {
        if(history[i].Role == "user") { lastUserIdx = i; break; }
      }

      if(lastUserIdx < 0)
      {
        return;
      }

      for(int i = 0; i < lastUserIdx; i++)
      {
        if(history[i].Role == "user" && history[i].Content is List<ContentPart> parts)
        {
          string text = parts.FirstOrDefault(p => p.Type == "text")?.Text ?? "[image]";
          history[i] = ChatMessage.User(text);
        }
      }
    }

    private static List<ChatMessage> BuildSummarizationRequest(List<ChatMessage> messages)
    {
      StringBuilder sb = new();
      sb.AppendLine("Summarize the following conversation concisely, preserving all important context, facts, and decisions:");
      sb.AppendLine();

      foreach(ChatMessage msg in messages)
      {
        switch(msg.Role)
        {
          case "user":
            sb.AppendLine($"User: {msg.TextContent ?? "[media]"}");
            break;
          case "assistant":
            sb.AppendLine($"Assistant: {msg.TextContent}");
            break;
        }
      }

      return [ChatMessage.User(sb.ToString())];
    }
  }
}
