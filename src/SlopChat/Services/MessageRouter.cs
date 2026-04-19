using Microsoft.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Handlers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SlopChat.Services;

public class MessageRouter
{
    private readonly BotOptions _options;
    private readonly SlopMessageHandler _slopHandler;
    private readonly CommandHandler _commandHandler;
    private readonly ILogger<MessageRouter> _logger;
    private static readonly string[] SlopPrefixes = { "slop", "\u0441\u043b\u043e\u043f" };

    public MessageRouter(
      BotOptions options,
      SlopMessageHandler slopHandler,
      CommandHandler commandHandler,
      ILogger<MessageRouter> logger
    )
    {
      _options = options;
      _slopHandler = slopHandler;
      _commandHandler = commandHandler;
      _logger = logger;
    }

    public async Task RouteAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
      if(message.From is null)
      {
        return;
      }

      string? messageText = message.Text ?? message.Caption;
      if(string.IsNullOrEmpty(messageText))
      {
        return;
      }

      if(!IsAuthorized(message))
      {
        return;
      }

      string text = messageText.Trim();

      if(text.Equals("!reset", StringComparison.OrdinalIgnoreCase))
      {
        await _commandHandler.HandleResetAsync(bot, message, ct);
        return;
      }

      if(text.Equals("!model", StringComparison.OrdinalIgnoreCase))
      {
        await _commandHandler.HandleModelAsync(bot, message, ct);
        return;
      }

      if(text.Equals("!models", StringComparison.OrdinalIgnoreCase))
      {
        if(!IsAdmin(message))
        {
          return;
        }

        await _commandHandler.HandleModelsAsync(bot, message, ct);
        return;
      }

      if(text.Equals("!version", StringComparison.OrdinalIgnoreCase))
      {
        if(!IsAdmin(message))
        {
          return;
        }

        await _commandHandler.HandleVersionAsync(bot, message, ct);
        return;
      }

      if(text.StartsWith("!set_model", StringComparison.OrdinalIgnoreCase))
      {
        if(!IsAdmin(message))
        {
          return;
        }

        string modelName = text["!set_model".Length..].Trim();
        if(!string.IsNullOrEmpty(modelName))
        {
          await _commandHandler.HandleSetModelAsync(bot, message, modelName, ct);
        }

        return;
      }

      string? userText = TryStripSlopPrefix(text);
      if(userText is not null)
      {
        _logger.LogInformation("Slop message from {UserId} in chat {ChatId}", message.From.Id, message.Chat.Id);

        string? replyContext = null;
        PhotoSize[]? replyPhotos = null;
        Message? reply = message.ReplyToMessage;
        if(reply is not null)
        {
          replyContext = reply.Text ?? reply.Caption;
          replyPhotos = reply.Photo;
          if(replyContext is not null && reply.From is not null)
          {
            string name = reply.From.FirstName ?? reply.From.Username ?? "Unknown";
            replyContext = $"{name}: {replyContext}";
          }
        }

        PhotoSize[]? directPhotos = message.Photo;

        await _slopHandler.HandleAsync(bot, message, userText, ct, replyContext, replyPhotos, directPhotos);
      }
    }

    private bool IsAuthorized(Message message)
    {
      ChatType chatType = message.Chat.Type;

      if(chatType == ChatType.Private)
      {
        return IsAdmin(message);
      }

      if(chatType == ChatType.Group || chatType == ChatType.Supergroup)
      {
        return _options.AllowedChats.Contains(message.Chat.Id);
      }

      return false;
    }

    private bool IsAdmin(Message message) => message.From?.Id == _options.AdminId;

    private static string? TryStripSlopPrefix(string text)
    {
      foreach(string prefix in SlopPrefixes)
      {
        if(text.Length >= prefix.Length
           && text[..prefix.Length].Equals(prefix, StringComparison.OrdinalIgnoreCase))
        {
          string remainder = text[prefix.Length..].TrimStart();
          return remainder.Length > 0 ? remainder : null;
        }
      }

      return null;
    }
  }