using Microsoft.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Handlers
{
  public class CommandHandler
  {
    private readonly OpenRouterClient _openRouter;
    private readonly ConversationManager _conversationManager;
    private readonly BotOptions _options;
    private readonly ILogger<CommandHandler> _logger;

    public CommandHandler(
      OpenRouterClient openRouter,
      ConversationManager conversationManager,
      BotOptions options,
      ILogger<CommandHandler> logger
    )
    {
      _openRouter = openRouter;
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

      await bot.SendChatAction(chatId, Telegram.Bot.Types.Enums.ChatAction.UploadPhoto, cancellationToken: ct);

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
        string caption = fullPrompt.Length > 1024 ? fullPrompt[..1021] + "..." : fullPrompt;

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
        await TelegramMessageHelper.SendChunkedAsync(bot, chatId, result.TextResponse!, message.MessageId, ct);
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
  }
}