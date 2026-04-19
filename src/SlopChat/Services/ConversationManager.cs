using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using SlopChat.Configuration;

namespace SlopChat.Services;

public class ConversationManager
{
    private readonly ConcurrentDictionary<long, List<ChatMessage>> _histories = new();
    private readonly ConcurrentDictionary<long, string> _models = new();
    private readonly ConcurrentDictionary<long, bool> _compacting = new();
    private readonly OpenRouterClient _openRouter;
    private readonly BotOptions _options;
    private readonly ILogger<ConversationManager> _logger;
    private readonly Lock _lock = new();
    private const int CompactThresholdPairs = 40;
    private const int KeepRecentPairs = 10;

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
        GetHistory(chatId).Add(ChatMessage.CreateUserMessage(content));
      }
    }

    public void AddAssistantMessage(long chatId, string content)
    {
      lock(_lock)
      {
        GetHistory(chatId).Add(ChatMessage.CreateAssistantMessage(content));
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
        string model = GetModel(chatId);
        List<ChatMessage> request = BuildSummarizationRequest(toSummarize);
        string summary = await _openRouter.GetCompletionAsync(request, model, ct);

        lock(_lock)
        {
          List<ChatMessage> history = GetHistory(chatId);
          history.RemoveRange(1, summarizeCount);
          history.Insert(1, ChatMessage.CreateAssistantMessage($"Summary of previous conversation:\n{summary}"));
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

    private List<ChatMessage> CreateInitialHistory() => [ChatMessage.CreateSystemMessage(_options.SystemPrompt)];

    private static List<ChatMessage> BuildSummarizationRequest(List<ChatMessage> messages)
    {
      StringBuilder sb = new();
      sb.AppendLine("Summarize the following conversation concisely, preserving all important context, facts, and decisions:");
      sb.AppendLine();

      foreach(ChatMessage msg in messages)
      {
        if(msg is UserChatMessage user)
        {
          string text = string.Concat(user.Content.Select(p => p.Text ?? ""));
          sb.AppendLine($"User: {text}");
        }
        else if(msg is AssistantChatMessage assistant)
        {
          string text = string.Concat(assistant.Content?.Select(p => p.Text ?? "") ?? []);
          sb.AppendLine($"Assistant: {text}");
        }
      }

      return [ChatMessage.CreateUserMessage(sb.ToString())];
    }
  }
