using Microsoft.Extensions.Logging;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Handlers;

public class SlopMessageHandler
{
    private readonly OpenRouterClient _openRouter;
    private readonly ConversationManager _conversationManager;
    private readonly ILogger<SlopMessageHandler> _logger;

    public SlopMessageHandler(
      OpenRouterClient openRouter,
      ConversationManager conversationManager,
      ILogger<SlopMessageHandler> logger)
    {
      _openRouter = openRouter;
      _conversationManager = conversationManager;
      _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Message message, string userText, CancellationToken ct)
    {
      long chatId = message.Chat.Id;

      _conversationManager.AddUserMessage(chatId, userText);
      var history = _conversationManager.GetSnapshot(chatId);

      try
      {
        string response = await _openRouter.GetCompletionAsync(history, ct);
        _conversationManager.AddAssistantMessage(chatId, response);
        await SendChunkedAsync(bot, chatId, response, message.MessageId, ct);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Error getting completion for chat {ChatId}", chatId);
        await bot.SendMessage(chatId, "Something went wrong while getting a response.", replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct);
      }
    }

    private static async Task SendChunkedAsync(ITelegramBotClient bot, long chatId, string text, int replyToMessageId, CancellationToken ct)
    {
      const int maxLength = 4096;

      if(text.Length <= maxLength)
      {
        await bot.SendMessage(chatId, text, replyParameters: new ReplyParameters { MessageId = replyToMessageId }, cancellationToken: ct);
        return;
      }

      int offset = 0;
      bool isFirst = true;
      while(offset < text.Length)
      {
        int length = Math.Min(maxLength, text.Length - offset);

        if(offset + length < text.Length)
        {
          int newlineIndex = text.LastIndexOf('\n', offset + length - 1, length);
          if(newlineIndex > offset)
          {
            length = newlineIndex - offset + 1;
          }
        }

        string chunk = text.Substring(offset, length);
        ReplyParameters? replyParams = isFirst ? new ReplyParameters { MessageId = replyToMessageId } : null;
        await bot.SendMessage(chatId, chunk, replyParameters: replyParams, cancellationToken: ct);

        offset += length;
        isFirst = false;
      }
    }
  }