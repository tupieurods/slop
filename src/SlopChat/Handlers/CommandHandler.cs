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
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
      OpenRouterClient openRouter,
      OpenRouterVideoClient openRouterVideo,
      ConversationManager conversationManager,
      BotOptions options,
      ILogger<CommandHandler> logger
    )
    {
      _openRouter = openRouter;
      _openRouterVideo = openRouterVideo;
      _conversationManager = conversationManager;
      _options = options;
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

    public async Task HandleModelsAsync(ITelegramBotClient bot, Message message, string filter, CancellationToken ct)
    {
      var models = await _openRouter.GetModelsAsync(ct);
      await SendFilteredModelsAsync(
        bot,
        message,
        filter,
        models,
        noun: "models",
        fetchFailedMessage: "Failed to fetch models list.",
        matches: (m, f) => m.Contains(f, StringComparison.OrdinalIgnoreCase),
        renderLine: m => $"  {m}",
        ct
      );
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

    public async Task HandleDrawModelsAsync(ITelegramBotClient bot, Message message, string filter, CancellationToken ct)
    {
      var models = await _openRouter.GetImageModelsAsync(ct);
      await SendFilteredModelsAsync(
        bot,
        message,
        filter,
        models,
        noun: "image generation models",
        fetchFailedMessage: "Failed to fetch image models list.",
        matches: (m, f) => m.Id.Contains(f, StringComparison.OrdinalIgnoreCase),
        renderLine: m => $"  {m.Id}{(m.CanOutputText ? " (text+image)" : " (image only)")}",
        ct
      );
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

    public async Task HandleVideoModelsAsync(ITelegramBotClient bot, Message message, string filter, CancellationToken ct)
    {
      var models = await _openRouterVideo.GetVideoModelsAsync(ct);
      await SendFilteredModelsAsync(
        bot,
        message,
        filter,
        models,
        noun: "video generation models",
        fetchFailedMessage: "Failed to fetch video models list.",
        matches: (m, f) =>
          m.Id.Contains(f, StringComparison.OrdinalIgnoreCase) ||
          (!string.IsNullOrEmpty(m.Name) && m.Name.Contains(f, StringComparison.OrdinalIgnoreCase)),
        renderLine: m => string.IsNullOrEmpty(m.Name) ? $"  {m.Id}" : $"  {m.Id} — {m.Name}",
        ct
      );
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

    public async Task HandleSummaryModelAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      await bot.SendMessage(
        message.Chat.Id,
        $"Current summary model: {_conversationManager.GetSummaryModel()}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct
      );
    }

    public async Task HandleSetSummaryModelAsync(ITelegramBotClient bot, Message message, string modelName, CancellationToken ct)
    {
      _conversationManager.SetSummaryModel(modelName);
      _logger.LogInformation("Summary model changed to {Model} by admin {UserId}", modelName, message.From?.Id);
      await bot.SendMessage(
        message.Chat.Id,
        $"Summary model set to: {modelName}",
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

      try
      {
        string? firstFrameDataUrl = null;
        if(replyPhotos is { Length: > 0 })
        {
          firstFrameDataUrl = await TelegramMediaDownloader.DownloadPhotoAsDataUrlAsync(bot, replyPhotos, ct);
        }

        var result = await _openRouterVideo.GenerateVideoAsync(fullPrompt, videoModel, firstFrameDataUrl, ct);

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
            cancellationToken: ct
          );
        }
        else
        {
          string errorText = result.ErrorMessage ?? "Failed to generate video.";
          await bot.SendMessage(
            chatId,
            errorText,
            replyParameters: new ReplyParameters { MessageId = originalMessageId },
            cancellationToken: ct
          );
        }

        try
        {
          await bot.DeleteMessage(chatId, statusMessageId, ct);
        }
        catch(Exception delEx)
        {
          _logger.LogWarning(delEx, "Failed to delete status message in chat {ChatId}", chatId);
        }
      }
      catch(OperationCanceledException) when(ct.IsCancellationRequested)
      {
        _logger.LogInformation("Video generation cancelled on shutdown for chat {ChatId}", chatId);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Video generation error for chat {ChatId}", chatId);
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
    }

    private static async Task SendFilteredModelsAsync<T>(
      ITelegramBotClient bot,
      Message message,
      string filter,
      IReadOnlyList<T> models,
      string noun,
      string fetchFailedMessage,
      Func<T, string, bool> matches,
      Func<T, string> renderLine,
      CancellationToken ct
    )
    {
      if(models.Count == 0)
      {
        await bot.SendMessage(
          message.Chat.Id,
          fetchFailedMessage,
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      string trimmedFilter = filter.Trim();
      var filtered = trimmedFilter.Length > 0
        ? models.Where(m => matches(m, trimmedFilter)).ToList()
        : models.ToList();

      if(filtered.Count == 0)
      {
        await bot.SendMessage(
          message.Chat.Id,
          $"No {noun} match \"{trimmedFilter}\".",
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct
        );
        return;
      }

      string header = trimmedFilter.Length > 0
        ? $"Available {noun} matching \"{trimmedFilter}\":\n\n"
        : $"Available {noun}:\n\n";
      string text = header + string.Join('\n', filtered.Select(renderLine));
      await TelegramMessageHelper.SendChunkedAsync(bot, message.Chat.Id, text, message.MessageId, ct);
    }
  }
}
