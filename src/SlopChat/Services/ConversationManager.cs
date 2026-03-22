using System.Collections.Concurrent;
using OpenAI.Chat;
using SlopChat.Configuration;

namespace SlopChat.Services
{
  public class ConversationManager
  {
    private readonly ConcurrentDictionary<long, List<ChatMessage>> _histories = new();
    private readonly BotOptions _options;
    private readonly object _lock = new();
    private const int MaxHistoryPairs = 50;

    public ConversationManager(BotOptions options)
    {
      _options = options;
    }

    public List<ChatMessage> GetHistory(long chatId)
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
        var history = GetHistory(chatId);
        history.Add(ChatMessage.CreateUserMessage(content));
        TrimHistory(history);
      }
    }

    public void AddAssistantMessage(long chatId, string content)
    {
      lock(_lock)
      {
        var history = GetHistory(chatId);
        history.Add(ChatMessage.CreateAssistantMessage(content));
        TrimHistory(history);
      }
    }

    public List<ChatMessage> GetSnapshot(long chatId)
    {
      lock(_lock)
      {
        var history = GetHistory(chatId);
        return new List<ChatMessage>(history);
      }
    }

    public void Reset(long chatId)
    {
      lock(_lock)
      {
        _histories[chatId] = CreateInitialHistory();
      }
    }

    private List<ChatMessage> CreateInitialHistory() => new() { ChatMessage.CreateSystemMessage(_options.SystemPrompt) };

    private static void TrimHistory(List<ChatMessage> history)
    {
      int maxMessages = 1 + MaxHistoryPairs * 2;
      if(history.Count <= maxMessages)
      {
        return;
      }

      ChatMessage systemMessage = history[0];
      int removeCount = history.Count - maxMessages;
      history.RemoveRange(1, removeCount);
      history[0] = systemMessage;
    }
  }
}
