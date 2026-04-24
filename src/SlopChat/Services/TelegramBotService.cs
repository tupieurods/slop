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
    private readonly OpenRouterClient _openRouter;
    private readonly OpenRouterVideoClient _openRouterVideo;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly ILogger<TelegramBotService> _logger;
    private CancellationTokenSource? _cts;

    public TelegramBotService(
      TelegramBotClient bot,
      MessageRouter router,
      OpenRouterClient openRouter,
      OpenRouterVideoClient openRouterVideo,
      IHostApplicationLifetime appLifetime,
      ILogger<TelegramBotService> logger
    )
    {
      _bot = bot;
      _router = router;
      _openRouter = openRouter;
      _openRouterVideo = openRouterVideo;
      _appLifetime = appLifetime;
      _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

      var models = await _openRouter.GetImageModelsAsync(cancellationToken);
      _logger.LogInformation("Loaded {Count} image generation models", models.Count);

      var videoModels = await _openRouterVideo.GetVideoModelsAsync(cancellationToken);
      _logger.LogInformation("Loaded {Count} video generation models", videoModels.Count);

      ReceiverOptions receiverOptions = new()
      {
        AllowedUpdates = [UpdateType.Message]
      };

      _bot.StartReceiving(
        HandleUpdateAsync,
        HandleErrorAsync,
        receiverOptions,
        _cts.Token
      );

      _logger.LogInformation("Telegram bot started receiving updates");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _logger.LogInformation("Telegram bot stopping");
      _cts?.Cancel();
      _cts?.Dispose();
      return Task.CompletedTask;
    }

    private Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
      if(update.Message is not { } message)
      {
        return Task.CompletedTask;
      }

      CancellationToken shutdownCt = _appLifetime.ApplicationStopping;

      _ = Task.Run(async () =>
      {
        try
        {
          await _router.RouteAsync(botClient, message, shutdownCt);
        }
        catch(OperationCanceledException) when(shutdownCt.IsCancellationRequested)
        {
          _logger.LogInformation("Message handling cancelled on shutdown for chat {ChatId}", message.Chat.Id);
        }
        catch(Exception ex)
        {
          _logger.LogError(ex, "Error handling message from chat {ChatId}", message.Chat.Id);
        }
      }, shutdownCt);

      return Task.CompletedTask;
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
      _logger.LogError(exception, "Telegram bot polling error");
      return Task.CompletedTask;
    }
  }