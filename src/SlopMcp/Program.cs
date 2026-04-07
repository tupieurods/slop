using SlopMcp.Services;
using SlopMcp.Tools;

var builder = WebApplication.CreateBuilder(args);

string searXngUrl = Environment.GetEnvironmentVariable("SLOP_SEARXNG_URL") ?? "http://searxng:8080";

builder.Services.AddHttpClient<SearXngClient>(client =>
{
  client.BaseAddress = new Uri(searXngUrl);
  client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services
  .AddMcpServer()
  .WithHttpTransport()
  .WithTools<WebSearchTool>();

var app = builder.Build();
app.MapMcp();
app.Run();
