using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class Crawl4AiClientTests
  {
    private static Crawl4AiClient CreateClient(HttpMessageHandler handler, string token = "test-token")
    {
      var httpClient = new HttpClient(handler)
      {
        BaseAddress = new Uri("http://crawl4ai:11235/"),
        Timeout = TimeSpan.FromSeconds(10)
      };
      httpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
      return new Crawl4AiClient(httpClient, NullLogger<Crawl4AiClient>.Instance, TimeProvider.System);
    }

    private static StringContent JsonContent(string json)
      => new StringContent(json, Encoding.UTF8, "application/json");

    [Fact]
    public async Task SubmitCrawlJobAsync_Success_ReturnsTaskId()
    {
      const string json = """{"task_id":"abc123","status":"pending"}""";

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) }
      );

      var client = CreateClient(handler);
      var outcome = await client.SubmitCrawlJobAsync("https://example.com", "http://callback/");

      Assert.Equal("abc123", outcome.TaskId);
      Assert.Equal(HttpStatusCode.OK, outcome.HttpStatus);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SubmitCrawlJobAsync_Non2xx_ReturnsStatus()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = JsonContent("{}") }
      );

      var client = CreateClient(handler);
      var outcome = await client.SubmitCrawlJobAsync("https://example.com", "http://callback/");

      Assert.Equal(HttpStatusCode.Unauthorized, outcome.HttpStatus);
      Assert.Null(outcome.TaskId);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SubmitCrawlJobAsync_TransportError_ReturnsError()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        throw new HttpRequestException("Connection refused")
      );

      var client = CreateClient(handler);
      var outcome = await client.SubmitCrawlJobAsync("https://example.com", "http://callback/");

      Assert.NotNull(outcome.TransportError);
      Assert.Null(outcome.TaskId);
      Assert.Null(outcome.HttpStatus);
    }

    [Fact]
    public async Task SubmitCrawlJobAsync_SendsBearerToken()
    {
      const string json = """{"task_id":"xyz"}""";
      string? authHeader = null;

      var handler = new FuncHttpMessageHandler(req =>
      {
        authHeader = req.Headers.Authorization?.ToString();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) };
      });

      var client = CreateClient(handler, "my-secret-token");
      await client.SubmitCrawlJobAsync("https://example.com", "http://callback/");

      Assert.Equal("Bearer my-secret-token", authHeader);
    }

    [Fact]
    public async Task SubmitCrawlJobAsync_PostsToCorrectEndpoint()
    {
      const string json = """{"task_id":"xyz"}""";
      string? requestPath = null;

      var handler = new FuncHttpMessageHandler(req =>
      {
        requestPath = req.RequestUri?.PathAndQuery;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) };
      });

      var client = CreateClient(handler);
      await client.SubmitCrawlJobAsync("https://example.com", "http://callback/");

      Assert.Equal("/crawl/job", requestPath);
    }
  }
}
