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
  private static readonly string[] SlopPrefixes = ["slop", "слоп"];

  private delegate Task CommandAction(ITelegramBotClient bot, Message message, string args, CancellationToken ct);

  private readonly Dictionary<string, (CommandAction Action, bool AdminOnly)> _commands;

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

    _commands = new(StringComparer.OrdinalIgnoreCase)
    {
      ["!reset"] = (HandleResetCommand, false),
      ["!model"] = (HandleModelCommand, false),
      ["!models"] = (HandleModelsCommand, true),
      ["!version"] = (HandleVersionCommand, true),
      ["!set_model"] = (HandleSetModelCommand, true),
    };
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

    if(text.StartsWith('!') && TryRouteCommand(text, out string command, out string args, out var entry))
    {
      if(entry.AdminOnly && !IsAdmin(message))
      {
        return;
      }

      await entry.Action(bot, message, args, ct);
      return;
    }

    string? userText = TryStripSlopPrefix(text);
    if(userText is not null)
    {
      await HandleSlopMessageAsync(bot, message, userText, ct);
    }
  }

  private bool TryRouteCommand(
    string text,
    out string command,
    out string args,
    out (CommandAction Action, bool AdminOnly) entry)
  {
    int spaceIndex = text.IndexOf(' ');
    if(spaceIndex >= 0)
    {
      command = text[..spaceIndex];
      args = text[(spaceIndex + 1)..].Trim();
    }
    else
    {
      command = text;
      args = string.Empty;
    }

    return _commands.TryGetValue(command, out entry);
  }

  private async Task HandleResetCommand(ITelegramBotClient bot, Message message, string args, CancellationToken ct)
  {
    await _commandHandler.HandleResetAsync(bot, message, ct);
  }

  private async Task HandleModelCommand(ITelegramBotClient bot, Message message, string args, CancellationToken ct)
  {
    await _commandHandler.HandleModelAsync(bot, message, ct);
  }

  private async Task HandleModelsCommand(ITelegramBotClient bot, Message message, string args, CancellationToken ct)
  {
    await _commandHandler.HandleModelsAsync(bot, message, ct);
  }

  private async Task HandleVersionCommand(ITelegramBotClient bot, Message message, string args, CancellationToken ct)
  {
    await _commandHandler.HandleVersionAsync(bot, message, ct);
  }

  private async Task HandleSetModelCommand(ITelegramBotClient bot, Message message, string args, CancellationToken ct)
  {
    if(!string.IsNullOrEmpty(args))
    {
      await _commandHandler.HandleSetModelAsync(bot, message, args, ct);
    }
  }

  private async Task HandleSlopMessageAsync(ITelegramBotClient bot, Message message, string userText, CancellationToken ct)
  {
    _logger.LogInformation("Slop message from {UserId} in chat {ChatId}", message.From!.Id, message.Chat.Id);

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
