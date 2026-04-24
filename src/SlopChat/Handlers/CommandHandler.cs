using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SlopChat.Handlers
{
  public class CommandHandler
  {
    private readonly OpenRouterClient _openRouter;
    private readonly OpenRouterVideoClient _openRouterVideo;
    private readonly ConversationManager _conversationManager;
    private readonly BotOptions _options;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
      OpenRouterClient openRouter,
      OpenRouterVideoClient openRouterVideo,
      ConversationManager conversationManager,
      BotOptions options,
      IHostApplicationLifetime appLifetime,
      ILogger<CommandHandler> logger
    )
    {
      _openRouter = openRouter;
      _openRouterVideo = openRouterVideo;
      _conversationManager = conversationManager;
      _options = options;
      _appLifetime = appLifetime;
      _logger = logger;
    }

    public async Task HandleResetAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      _conversationManager.Reset(message.Chat.Id);
      await bot.SendMessage(
        message.Chat.Id,
        "Context has been reset.",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleModelAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      await bot.SendMessage(
        message.Chat.Id,
        $"Current model: {_conversationManager.GetModel(message.Chat.Id)}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleSetModelAsync(ITelegramBotClient bot, Message message, string modelName, CancellationToken ct)
    {
      _conversationManager.SetModel(message.Chat.Id, modelName);
      _conversationManager.Reset(message.Chat.Id);
      _logger.LogInformation("Model changed to {Model} in chat {ChatId} by admin {UserId}", modelName, message.Chat.Id, message.From?.Id);
      await bot.SendMessage(
        message.Chat.Id,
        $"Model set to: {modelName}\nContext has been reset.",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleModelsAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      var models = await _openRouter.GetModelsAsync(ct);

      if(models.Count == 0)
      {
        await bot.SendMessage(
          message.Chat.Id,
          "Failed to fetch models list.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      string text = "Available models:\n\n" + string.Join('\n', models.Select(m => $"  {m}"));
      await TelegramMessageHelper.SendChunkedAsync(bot, message.Chat.Id, text, message.MessageId, ct);
    }

    public async Task HandleVersionAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      await bot.SendMessage(
        message.Chat.Id,
        $"Build time: {_options.BuildTime}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleDrawModelsAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      var models = await _openRouter.GetImageModelsAsync(ct);

      if(models.Count == 0)
      {
        await bot.SendMessage(
          message.Chat.Id,
          "Failed to fetch image models list.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      string text = "Available image generation models:\n\n" +
        string.Join('\n', models.Select(m => $"  {m.Id}{(m.CanOutputText ? " (text+image)" : " (image only)")}"));
      await TelegramMessageHelper.SendChunkedAsync(bot, message.Chat.Id, text, message.MessageId, ct);
    }

    public async Task HandleSetDrawModelAsync(ITelegramBotClient bot, Message message, string modelName, CancellationToken ct)
    {
      _conversationManager.SetDrawModel(message.Chat.Id, modelName);
      _logger.LogInformation(
        "Draw model changed to {Model} in chat {ChatId} by admin {UserId}",
        modelName,
        message.Chat.Id,
        message.From?.Id
      );
      await bot.SendMessage(
        message.Chat.Id,
        $"Draw model set to: {modelName}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleDrawAsync(
      ITelegramBotClient bot,
      Message message,
      string prompt,
      CancellationToken ct,
      string? replyContext = null,
      PhotoSize[]? replyPhotos = null
    )
    {
      long chatId = message.Chat.Id;
      string drawModel = _conversationManager.GetDrawModel(chatId);

      string fullPrompt = string.IsNullOrEmpty(prompt) && replyContext is not null
        ? replyContext
        : replyContext is not null
          ? $"{prompt}\n\nContext from replied message: {replyContext}"
          : prompt;

      if(string.IsNullOrWhiteSpace(fullPrompt))
      {
        await bot.SendMessage(
          chatId,
          "Please provide a prompt for image generation.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      await bot.SendChatAction(chatId, ChatAction.UploadPhoto, cancellationToken: ct);

      Models.ImageGenerationResult result;

      if(replyPhotos is { Length: > 0 })
      {
        string? imageDataUrl = await TelegramMediaDownloader.DownloadPhotoAsDataUrlAsync(bot, replyPhotos, ct);
        if(imageDataUrl is not null)
        {
          result = await _openRouter.GenerateImageFromImageAsync(fullPrompt, drawModel, imageDataUrl, ct);
        }
        else
        {
          result = await _openRouter.GenerateImageAsync(fullPrompt, drawModel, ct);
        }
      }
      else
      {
        result = await _openRouter.GenerateImageAsync(fullPrompt, drawModel, ct);
      }

      if(result.HasImage)
      {
        using var stream = new MemoryStream(result.ImageBytes!);
        string costStr = result.Cost.HasValue ? $"${result.Cost.Value:F4}" : "unknown";
        string caption = $"{drawModel}: {costStr}";

        await bot.SendPhoto(
          chatId,
          InputFile.FromStream(stream, "generated.png"),
          caption: caption,
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      if(result.HasText)
      {
        string costStr = result.Cost.HasValue ? $"${result.Cost.Value:F4}" : "unknown";
        string textWithCost = $"{drawModel}: {costStr}\n\n{result.TextResponse!}";
        await TelegramMessageHelper.SendChunkedAsync(bot, chatId, textWithCost, message.MessageId, ct);
        return;
      }

      string errorText = result.ErrorMessage ?? "Failed to generate image. The model may not support this operation.";
      await bot.SendMessage(
        chatId,
        errorText,
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleVideoModelsAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      var models = await _openRouterVideo.GetVideoModelsAsync(ct);

      if(models.Count == 0)
      {
        await bot.SendMessage(
          message.Chat.Id,
          "Failed to fetch video models list.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      string text = "Available video generation models:\n\n" +
        string.Join('\n', models.Select(m => string.IsNullOrEmpty(m.Name) ? $"  {m.Id}" : $"  {m.Id} — {m.Name}"));
      await TelegramMessageHelper.SendChunkedAsync(bot, message.Chat.Id, text, message.MessageId, ct);
    }

    public async Task HandleSetVideoModelAsync(ITelegramBotClient bot, Message message, string modelName, CancellationToken ct)
    {
      _conversationManager.SetVideoModel(message.Chat.Id, modelName);
      _logger.LogInformation(
        "Video model changed to {Model} in chat {ChatId} by admin {UserId}",
        modelName,
        message.Chat.Id,
        message.From?.Id
      );
      await bot.SendMessage(
        message.Chat.Id,
        $"Video model set to: {modelName}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleVideoAsync(
      ITelegramBotClient bot,
      Message message,
      string prompt,
      CancellationToken ct,
      string? replyContext = null,
      PhotoSize[]? replyPhotos = null
    )
    {
      long chatId = message.Chat.Id;
      string videoModel = _conversationManager.GetVideoModel(chatId);

      string fullPrompt = string.IsNullOrEmpty(prompt) && replyContext is not null
        ? replyContext
        : replyContext is not null
          ? $"{prompt}\n\nContext from replied message: {replyContext}"
          : prompt;

      if(string.IsNullOrWhiteSpace(fullPrompt))
      {
        await bot.SendMessage(
          chatId,
          "Please provide a prompt for video generation.",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      await bot.SendChatAction(chatId, ChatAction.UploadVideo, cancellationToken: ct);

      Message statusMessage = await bot.SendMessage(
        chatId,
        "Generating video...",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );

      int originalMessageId = message.MessageId;
      int statusMessageId = statusMessage.MessageId;

      CancellationToken shutdownCt = _appLifetime.ApplicationStopping;

      _ = Task.Run(async () =>
      {
        try
        {
          string? firstFrameDataUrl = null;
          if(replyPhotos is { Length: > 0 })
          {
            firstFrameDataUrl = await TelegramMediaDownloader.DownloadPhotoAsDataUrlAsync(bot, replyPhotos, shutdownCt);
          }

          var result = await _openRouterVideo.GenerateVideoAsync(fullPrompt, videoModel, firstFrameDataUrl, shutdownCt);

          if(result.HasVideo)
          {
            string costStr = result.Cost.HasValue ? $"${result.Cost.Value:F4}" : "unknown";
            string caption = $"{videoModel}: {costStr}";

            using var stream = new MemoryStream(result.VideoBytes!);
            await bot.SendVideo(
              chatId,
              InputFile.FromStream(stream, "generated.mp4"),
              caption: caption,
              replyParameters: new ReplyParameters { MessageId = originalMessageId },
              cancellationToken: shutdownCt
            );
          }
          else
          {
            string errorText = result.ErrorMessage ?? "Failed to generate video.";
            await bot.SendMessage(
              chatId,
              errorText,
              replyParameters: new ReplyParameters { MessageId = originalMessageId },
              cancellationToken: shutdownCt
            );
          }

          try
          {
            await bot.DeleteMessage(chatId, statusMessageId, shutdownCt);
          }
          catch(Exception delEx)
          {
            _logger.LogWarning(delEx, "Failed to delete status message in chat {ChatId}", chatId);
          }
        }
        catch(OperationCanceledException) when(shutdownCt.IsCancellationRequested)
        {
          _logger.LogInformation("Video generation task cancelled on shutdown for chat {ChatId}", chatId);
        }
        catch(Exception ex)
        {
          _logger.LogError(ex, "Background video generation error for chat {ChatId}", chatId);
          try
          {
            await bot.SendMessage(
              chatId,
              $"Video generation error: {ex.Message}",
              replyParameters: new ReplyParameters { MessageId = originalMessageId },
              cancellationToken: CancellationToken.None
            );
          }
          catch(Exception innerEx)
          {
            _logger.LogError(innerEx, "Failed to send video error message to chat {ChatId}", chatId);
          }

          try
          {
            await bot.DeleteMessage(chatId, statusMessageId, CancellationToken.None);
          }
          catch(Exception delEx)
          {
            _logger.LogWarning(delEx, "Failed to delete status message in chat {ChatId}", chatId);
          }
        }
      });
    }
  }
}
