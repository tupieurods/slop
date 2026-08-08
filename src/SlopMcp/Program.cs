using SlopMcp.Configuration;
using SlopMcp.Services;
using SlopMcp.Tools;

namespace SlopMcp;

internal class Program
{
  private static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    string searXngUrl = Environment.GetEnvironmentVariable("SLOP_SEARXNG_URL") ?? "http://searxng:8080";

    string crawl4aiToken = Environment.GetEnvironmentVariable("SLOP_CRAWL4AI_TOKEN")
      ?? throw new InvalidOperationException("SLOP_CRAWL4AI_TOKEN environment variable is required but not set.");

    var crawl4aiOptions = new Crawl4AiOptions
    {
      BaseUrl = Environment.GetEnvironmentVariable("SLOP_CRAWL4AI_URL") ?? "http://crawl4ai:11235",
      Token = crawl4aiToken,
      CallbackUrl = Environment.GetEnvironmentVariable("SLOP_CRAWL4AI_CALLBACK_URL") ?? "http://slopmcp:8080/internal/crawl4ai-callback",
      TimeoutSeconds = int.TryParse(Environment.GetEnvironmentVariable("SLOP_CRAWL4AI_TIMEOUT_SECONDS"), out int t) ? t : 90
    };

    builder.Services.AddSingleton(crawl4aiOptions);
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton<Crawl4AiJobRegistry>();
    builder.Services.AddSingleton<Crawl4AiCallbackHandler>(sp => new Crawl4AiCallbackHandler(
      sp.GetRequiredService<Crawl4AiJobRegistry>(),
      sp.GetRequiredService<ILogger<Crawl4AiCallbackHandler>>(),
      crawl4aiToken
    ));

    builder.Services.AddHttpClient<SearXngClient>(client =>
    {
      client.BaseAddress = new Uri(searXngUrl);
      client.Timeout = TimeSpan.FromSeconds(15);
    });

    builder.Services.AddHttpClient<ImageUrlValidator>(client =>
    {
      client.Timeout = TimeSpan.FromSeconds(30);
      client.DefaultRequestHeaders.UserAgent.ParseAdd("SlopChat-ImageValidator/1.0");
    });

    builder.Services.AddHttpClient<Crawl4AiClient>(client =>
    {
      client.BaseAddress = new Uri(crawl4aiOptions.BaseUrl.TrimEnd('/') + "/");
      client.Timeout = TimeSpan.FromSeconds(10);
      client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", crawl4aiToken);
    });

    builder.Services
      .AddMcpServer()
      .WithHttpTransport()
      .WithTools<WebSearchTool>()
      .WithTools<ImageSearchTool>()
      .WithTools<GetCurrentDateTool>()
      .WithTools<FetchUrlTool>();

    var app = builder.Build();

    app.Logger.LogInformation(
      "Crawl4AI config: baseUrl={BaseUrl}, callbackUrl={CallbackUrl}, timeoutSeconds={TimeoutSeconds}",
      crawl4aiOptions.BaseUrl, crawl4aiOptions.CallbackUrl, crawl4aiOptions.TimeoutSeconds
    );

    app.MapPost("/internal/crawl4ai-callback", async (
      HttpContext context,
      Crawl4AiCallbackHandler handler
    ) =>
    {
      string? secret = context.Request.Query["secret"];
      using var reader = new System.IO.StreamReader(context.Request.Body);
      string body = await reader.ReadToEndAsync(context.RequestAborted);
      string ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
      return await handler.HandleAsync(secret, body, ip);
    });

    app.MapMcp();
    app.Run();
  }
}


