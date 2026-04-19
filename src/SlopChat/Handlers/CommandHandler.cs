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
  }
}