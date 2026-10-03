using Microsoft.Extensions.Logging;
using SlopChat.Models;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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

    public async Task HandleAsync(ITelegramBotClient bot, Message message, string userText, string senderLabel, CancellationToken ct, string? replyContext = null, PhotoSize[]? replyPhotos = null, PhotoSize[]? directPhotos = null)
    {
      long chatId = message.Chat.Id;

      ChatMessage userMessage = await BuildUserMessageAsync(bot, userText, senderLabel, replyContext, replyPhotos, directPhotos, ct);
      _conversationManager.AddMessage(chatId, userMessage);
      await _conversationManager.CompactIfNeededAsync(chatId, ct);
      var history = _conversationManager.GetSnapshot(chatId);

      try
      {
        CompletionResult completion = await _openRouter.GetCompletionWithMediaAsync(history, _conversationManager.GetModel(chatId), ct, _toolExecutor);
        string response = completion.Text;
        _conversationManager.AddAssistantMessage(chatId, response);
        await SendImagesAsync(bot, chatId, message.MessageId, completion.Images, ct);
        await TelegramMessageHelper.SendRichAsync(bot, chatId, response, message.MessageId, _logger, ct);
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

    private async Task SendImagesAsync(ITelegramBotClient bot, long chatId, int replyToMessageId, IReadOnlyList<byte[]> images, CancellationToken ct)
    {
      for(int i = 0; i < images.Count; i++)
      {
        string fileName = $"image_{i + 1}.png";
        try
        {
          using var stream = new MemoryStream(images[i]);
          await bot.SendPhoto(
            chatId,
            InputFile.FromStream(stream, fileName),
            replyParameters: new ReplyParameters { MessageId = replyToMessageId },
            cancellationToken: ct
          );
        }
        catch(ApiRequestException ex)
        {
          _logger.LogWarning(ex, "Failed to send tool image as photo to chat {ChatId}; retrying as document", chatId);
          await TrySendImageAsDocumentAsync(bot, chatId, replyToMessageId, images[i], fileName, ct);
        }
        catch(RequestException ex)
        {
          _logger.LogError(ex, "Failed to send tool image to chat {ChatId}", chatId);
        }
      }
    }

    private async Task TrySendImageAsDocumentAsync(
      ITelegramBotClient bot,
      long chatId,
      int replyToMessageId,
      byte[] image,
      string fileName,
      CancellationToken ct
    )
    {
      try
      {
        using var stream = new MemoryStream(image);
        await bot.SendDocument(
          chatId,
          InputFile.FromStream(stream, fileName),
          replyParameters: new ReplyParameters { MessageId = replyToMessageId },
          cancellationToken: ct
        );
      }
      catch(RequestException ex)
      {
        _logger.LogError(ex, "Failed to send tool image as document to chat {ChatId}", chatId);
      }
    }

    private static async Task<ChatMessage> BuildUserMessageAsync(
      ITelegramBotClient bot,
      string userText,
      string senderLabel,
      string? replyContext,
      PhotoSize[]? replyPhotos,
      PhotoSize[]? directPhotos,
      CancellationToken ct)
    {
      string labeledUserText = $"{senderLabel}: {userText}";
      bool hasPhotos = (replyPhotos is { Length: > 0 }) || (directPhotos is { Length: > 0 });

      if(!hasPhotos && replyContext is null)
      {
        return ChatMessage.User(labeledUserText);
      }

      string textContent = replyContext is not null
        ? $"[Replying to message: {replyContext}]\n{labeledUserText}"
        : labeledUserText;

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