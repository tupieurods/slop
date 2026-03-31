using System.Text;
using Microsoft.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Handlers;

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
      ILogger<CommandHandler> logger)
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
        cancellationToken: ct);
    }

    public async Task HandleModelAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      await bot.SendMessage(
        message.Chat.Id,
        $"Current model: {_options.DefaultModel}",
        replyParameters: new ReplyParameters { MessageId = message.MessageId },
        cancellationToken: ct);
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
          cancellationToken: ct);
        return;
      }

      StringBuilder sb = new();
      sb.AppendLine("Available models:");
      sb.AppendLine();

      foreach(string modelId in models)
      {
        sb.AppendLine($"  {modelId}");
      }

      string text = sb.ToString();

      if(text.Length <= 4096)
      {
        await bot.SendMessage(
          message.Chat.Id,
          text,
          replyParameters: new ReplyParameters { MessageId = message.MessageId },
          cancellationToken: ct);
        return;
      }

      int offset = 0;
      bool isFirst = true;
      while(offset < text.Length)
      {
        int length = Math.Min(4096, text.Length - offset);

        if(offset + length < text.Length)
        {
          int newlineIndex = text.LastIndexOf('\n', offset + length - 1, length);
          if(newlineIndex > offset)
          {
            length = newlineIndex - offset + 1;
          }
        }

        string chunk = text.Substring(offset, length);
        ReplyParameters? replyParams = isFirst ? new ReplyParameters { MessageId = message.MessageId } : null;
        await bot.SendMessage(message.Chat.Id, chunk, replyParameters: replyParams, cancellationToken: ct);

        offset += length;
        isFirst = false;
      }
    }
  }