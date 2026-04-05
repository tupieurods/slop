using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SlopChat.Services;

public class TelegramBotService: IHostedService
{
    private readonly TelegramBotClient _bot;
    private readonly MessageRouter _router;
    private readonly ILogger<TelegramBotService> _logger;
    private CancellationTokenSource? _cts;

    public TelegramBotService(
      TelegramBotClient bot,
      MessageRouter router,
      ILogger<TelegramBotService> logger
    )
    {
      _bot = bot;
      _router = router;
      _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

      ReceiverOptions receiverOptions = new()
      {
        AllowedUpdates = new[] { UpdateType.Message }
      };

      _bot.StartReceiving(
        HandleUpdateAsync,
        HandleErrorAsync,
        receiverOptions,
        _cts.Token
      );

      _logger.LogInformation("Telegram bot started receiving updates");
      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _logger.LogInformation("Telegram bot stopping");
      _cts?.Cancel();
      _cts?.Dispose();
      return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
      if(update.Message is not { } message)
      {
        return;
      }

      try
      {
        await _router.RouteAsync(botClient, message, ct);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Error handling message from chat {ChatId}", message.Chat.Id);
      }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
      _logger.LogError(exception, "Telegram bot polling error");
      return Task.CompletedTask;
    }
  }