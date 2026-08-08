using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SlopMcp.Configuration;
using SlopMcp.Services;
using SlopMcp.Tools;

namespace SlopChat.Tests
{
  public class FetchUrlToolTests
  {
    private static Crawl4AiOptions DefaultOptions(int timeoutSeconds = 90) =>
      new Crawl4AiOptions
      {
        BaseUrl = "http://crawl4ai:11235",
        Token = "test-secret",
        CallbackUrl = "http://slopmcp:8080/internal/crawl4ai-callback",
        TimeoutSeconds = timeoutSeconds
      };

    private static Crawl4AiJobRegistry CreateRegistry()
      => new Crawl4AiJobRegistry(NullLogger<Crawl4AiJobRegistry>.Instance, TimeProvider.System);

    private static Crawl4AiClient CreateClient(HttpMessageHandler handler)
    {
      var httpClient = new HttpClient(handler)
      {
        BaseAddress = new Uri("http://crawl4ai:11235/"),
        Timeout = TimeSpan.FromSeconds(10)
      };
      return new Crawl4AiClient(httpClient, NullLogger<Crawl4AiClient>.Instance, TimeProvider.System);
    }

    private static StringContent TaskIdJson(string taskId)
      => new StringContent($"{{\"task_id\":\"{taskId}\"}}", Encoding.UTF8, "application/json");

    [Fact]
    public async Task FetchAsync_InvalidUrl_ReturnsError()
    {
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync("not-a-url");

      Assert.Contains("Invalid URL", result);
    }

    [Fact]
    public async Task FetchAsync_FtpUrl_ReturnsError()
    {
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync("ftp://example.com/file.txt");

      Assert.Contains("Invalid URL", result);
    }

    [Fact]
    public async Task FetchAsync_TransportError_ReturnsError()
    {
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ => throw new HttpRequestException("refused")));
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync("https://example.com");

      Assert.Contains("error", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_SuccessRoundTrip_ReturnsFormattedMarkdown()
    {
      const string taskId = "test-task-42";
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = TaskIdJson(taskId) }
      ));

      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var fetchTask = tool.FetchAsync("https://example.com");
      await Task.Delay(50);
      registry.Complete(taskId, "Hello from page");

      var result = await fetchTask;

      Assert.StartsWith("# https://example.com", result);
      Assert.Contains("Hello from page", result);
    }

    [Fact]
    public async Task FetchAsync_LongMarkdown_TruncatesWithMarker()
    {
      const string taskId = "trunc-task";
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = TaskIdJson(taskId) }
      ));

      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);
      string longMarkdown = new string('x', 20_000);

      var fetchTask = tool.FetchAsync("https://example.com");
      await Task.Delay(50);
      registry.Complete(taskId, longMarkdown);

      var result = await fetchTask;

      Assert.Contains("...[truncated, original length: 20000 chars]", result);
      Assert.True(result.Length < 20_000 + 200);
    }

    [Fact]
    public async Task FetchAsync_Timeout_ReturnsTimeoutMessage()
    {
      const string taskId = "timeout-task";
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = TaskIdJson(taskId) }
      ));

      var options = new Crawl4AiOptions
      {
        BaseUrl = "http://crawl4ai:11235",
        Token = "test-secret",
        CallbackUrl = "http://slopmcp:8080/internal/crawl4ai-callback",
        TimeoutSeconds = 0
      };

      var tool = new FetchUrlTool(client, registry, options, NullLogger<FetchUrlTool>.Instance);
      var result = await tool.FetchAsync("https://example.com");

      Assert.True(result.StartsWith("Fetch failed:") || result.Contains("timed out") || result.Contains("cancelled"),
        $"Unexpected result: {result}");
    }

    [Fact]
    public async Task FetchAsync_JobFails_ReturnsFetchFailed()
    {
      const string taskId = "fail-task";
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = TaskIdJson(taskId) }
      ));

      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var fetchTask = tool.FetchAsync("https://example.com");
      await Task.Delay(50);
      registry.Fail(taskId, "crawl4ai returned failure status");

      var result = await fetchTask;

      Assert.StartsWith("Fetch failed:", result);
      Assert.Contains("crawl4ai returned failure status", result);
    }

    [Fact]
    public async Task FetchAsync_Non2xxWithBody_ReturnsEnrichedError()
    {
      var registry = CreateRegistry();
      const string responseBody = "{\"detail\":\"Invalid token\"}";
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
          Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        }
      );
      var client = CreateClient(handler);
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync("https://example.com");

      Assert.Contains("HTTP 400", result);
      Assert.Contains("Invalid token", result);
    }

    [Fact]
    public async Task FetchAsync_Non2xxEmptyBody_ReturnsFallbackError()
    {
      var registry = CreateRegistry();
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
          Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        }
      );
      var client = CreateClient(handler);
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync("https://example.com");

      Assert.Equal("Fetch backend error (HTTP 400). Try again later.", result);
    }

    [Theory]
    [InlineData("http://localhost/path")]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://crawl4ai/api")]
    [InlineData("http://slopmcp/")]
    [InlineData("http://searxng/")]
    [InlineData("http://slopchat/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://10.255.255.255/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://172.31.255.255/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://169.254.169.254/")]
    public async Task FetchAsync_InternalHost_ReturnsRefusal(string url)
    {
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var result = await tool.FetchAsync(url);

      Assert.Contains("Refusing to fetch internal or loopback address", result);
    }

    [Fact]
    public async Task FetchAsync_PublicUrl_IsAllowed()
    {
      const string taskId = "public-task";
      var registry = CreateRegistry();
      var client = CreateClient(new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = TaskIdJson(taskId) }
      ));

      var tool = new FetchUrlTool(client, registry, DefaultOptions(), NullLogger<FetchUrlTool>.Instance);

      var fetchTask = tool.FetchAsync("https://example.com");
      await Task.Delay(50);
      registry.Complete(taskId, "Public content");

      var result = await fetchTask;
      Assert.Contains("Public content", result);
    }

    [Theory]
    [InlineData("http://localhost/", "localhost")]
    [InlineData("http://127.0.0.1/", "127.0.0.1")]
    [InlineData("http://10.0.0.1/", "10.0.0.1")]
    [InlineData("http://172.16.1.1/", "172.16.1.1")]
    [InlineData("http://192.168.0.1/", "192.168.0.1")]
    [InlineData("http://169.254.1.1/", "169.254.1.1")]
    [InlineData("http://crawl4ai/", "crawl4ai")]
    public void IsInternalHost_RejectsKnownInternalHosts(string url, string _)
    {
      var uri = new Uri(url);
      Assert.True(FetchUrlTool.IsInternalHost(uri, out _));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("https://8.8.8.8/")]
    [InlineData("https://1.1.1.1/")]
    public void IsInternalHost_AllowsPublicHosts(string url)
    {
      var uri = new Uri(url);
      Assert.False(FetchUrlTool.IsInternalHost(uri, out _));
    }

    [Theory]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    [InlineData("http://localhost./")]
    [InlineData("http://LOCALHOST./")]
    [InlineData("http://127.0.0.2/")]
    [InlineData("http://127.1.2.3/")]
    [InlineData("http://[::ffff:127.0.0.5]/")]
    public void IsInternalHost_RejectsSSRFBypasses(string url)
    {
      var uri = new Uri(url);
      Assert.True(FetchUrlTool.IsInternalHost(uri, out _));
    }

    [Fact]
    public void FetchUrlTool_HasMcpServerToolTypeAttribute()
    {
      var attr = typeof(FetchUrlTool).GetCustomAttributes(typeof(McpServerToolTypeAttribute), inherit: false);
      Assert.NotEmpty(attr);
    }

    [Fact]
    public void FetchAsync_HasMcpServerToolAttributeWithCorrectName()
    {
      var method = typeof(FetchUrlTool).GetMethod(nameof(FetchUrlTool.FetchAsync));
      Assert.NotNull(method);
      var attrs = method!.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false);
      Assert.NotEmpty(attrs);
      var toolAttr = (McpServerToolAttribute)attrs[0];
      Assert.Equal("fetch_url", toolAttr.Name);
    }
  }
}
