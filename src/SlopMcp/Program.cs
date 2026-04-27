using SlopMcp.Services;
using SlopMcp.Tools;

namespace SlopMcp;

internal class Program
{
  private static void Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    string searXngUrl = Environment.GetEnvironmentVariable("SLOP_SEARXNG_URL") ?? "http://searxng:8080";

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

    builder.Services
      .AddMcpServer()
      .WithHttpTransport()
      .WithTools<WebSearchTool>()
      .WithTools<ImageSearchTool>()
      .WithTools<GetCurrentDateTool>();

    var app = builder.Build();
    app.MapMcp();
    app.Run();
  }
}
