using Microsoft.Extensions.Logging;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Handlers;

public class SlopMessageHandler
{
    private readonly OpenRouterClient _openRouter;
    private readonly ConversationManager _conversationManager;
    private readonly IToolExecutor? _toolExecutor;
    private readonly ILogger<SlopMessageHandler> _logger;

    public SlopMessageHandler(
      OpenRouterClient openRouter,
      ConversationManager conversationManager,
      IToolExecutor? toolExecutor,
      ILogger<SlopMessageHandler> logger
    )
    {
      _openRouter = openRouter;
      _conversationManager = conversationManager;
      _toolExecutor = toolExecutor;
      _logger = logger;
    }

    public async Task HandleAsync(ITelegramBotClient bot, Message message, string userText, CancellationToken ct)
    {
      long chatId = message.Chat.Id;

      _conversationManager.AddUserMessage(chatId, userText);
      await _conversationManager.CompactIfNeededAsync(chatId, ct);
      var history = _conversationManager.GetSnapshot(chatId);

      try
      {
        string response = await _openRouter.GetCompletionAsync(history, _conversationManager.GetModel(chatId), ct, _toolExecutor);
        _conversationManager.AddAssistantMessage(chatId, response);
        await TelegramMessageHelper.SendChunkedAsync(bot, chatId, response, message.MessageId, ct);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Error getting completion for chat {ChatId}", chatId);
        await bot.SendMessage(
          chatId,
          "Something went wrong while getting a response.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
      }
    }
  }