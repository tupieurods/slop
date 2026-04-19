using Microsoft.Extensions.Logging;
using SlopChat.Models;
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

    public async Task HandleAsync(ITelegramBotClient bot, Message message, string userText, CancellationToken ct, string? replyContext = null, PhotoSize[]? replyPhotos = null, PhotoSize[]? directPhotos = null)
    {
      long chatId = message.Chat.Id;

      ChatMessage userMessage = await BuildUserMessageAsync(bot, userText, replyContext, replyPhotos, directPhotos, ct);
      _conversationManager.AddMessage(chatId, userMessage);
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

    private static async Task<ChatMessage> BuildUserMessageAsync(
      ITelegramBotClient bot,
      string userText,
      string? replyContext,
      PhotoSize[]? replyPhotos,
      PhotoSize[]? directPhotos,
      CancellationToken ct)
    {
      bool hasPhotos = (replyPhotos is { Length: > 0 }) || (directPhotos is { Length: > 0 });

      if(!hasPhotos && replyContext is null)
      {
        return ChatMessage.User(userText);
      }

      string textContent = replyContext is not null
        ? $"[Replying to message: {replyContext}]\n{userText}"
        : userText;

      if(!hasPhotos)
      {
        return ChatMessage.User(textContent);
      }

      // Try downloading the photo
      PhotoSize[]? photos = directPhotos ?? replyPhotos;
      string? dataUrl = photos is not null
        ? await TelegramMediaDownloader.DownloadPhotoAsDataUrlAsync(bot, photos, ct)
        : null;

      // Fall back to text-only if download failed
      if(dataUrl is null)
      {
        return ChatMessage.User(textContent);
      }

      return ChatMessage.UserMultimodal(
      [
        ContentPart.TextContent(textContent),
        ContentPart.Image(dataUrl)
      ]);
    }
  }