using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using SlopChat.Configuration;
using SlopChat.Handlers;
using SlopChat.Services;
using Telegram.Bot;

namespace SlopChat;

internal class Program
{
  private static async Task Main(string[] args)
    {
      BotOptions options = BotOptions.FromEnvironment();

      if(string.IsNullOrEmpty(options.TelegramToken))
      {
        Console.Error.WriteLine("SLOP_TELEGRAM_TOKEN is not set.");
        Environment.Exit(1);
      }

      if(string.IsNullOrEmpty(options.OpenRouterKey))
      {
        Console.Error.WriteLine("SLOP_OPENROUTER_KEY is not set.");
        Environment.Exit(1);
      }

      IHost host = Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging =>
        {
          logging.ClearProviders();
          logging.AddNLog();
        })
        .ConfigureServices(services =>
        {
          services.AddSingleton(options);
          services.AddSingleton(new TelegramBotClient(options.TelegramToken));
          services.AddSingleton<ConversationManager>();
          services.AddSingleton<MessageRouter>();
          services.AddSingleton<SlopMessageHandler>();
          services.AddSingleton<CommandHandler>();
          services.AddSingleton(sp => new OpenRouterClient(
            options.OpenRouterKey,
            sp.GetRequiredService<ILogger<OpenRouterClient>>()
          ));

          if(!string.IsNullOrEmpty(options.McpServerUrl))
          {
            services.AddSingleton<IToolExecutor>(sp => new McpToolService(
              options.McpServerUrl,
              sp.GetRequiredService<ILoggerFactory>()
            ));
          }

          services.AddHostedService<TelegramBotService>();
        })
        .Build();

      await host.RunAsync();
    }
  }
