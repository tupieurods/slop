using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SlopChat.Configuration;
using SlopChat.Handlers;
using SlopChat.Services;
using Telegram.Bot;

namespace SlopChat
{
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
        .ConfigureServices(services =>
        {
          services.AddSingleton(options);
          services.AddSingleton(new TelegramBotClient(options.TelegramToken));
          services.AddSingleton<ConversationManager>();
          services.AddSingleton<MessageRouter>();
          services.AddSingleton<SlopMessageHandler>();
          services.AddSingleton<CommandHandler>();

          services.AddHttpClient<OpenRouterClient>(client =>
          {
            client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.OpenRouterKey);
            client.DefaultRequestHeaders.Add("X-Title", "SlopChat");
          });

          services.AddHostedService<TelegramBotService>();
        })
        .Build();

      await host.RunAsync();
    }
  }
}