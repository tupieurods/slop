using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class Crawl4AiCallbackHandlerTests
  {
    private const string ValidToken = "test-secret-token";

    private static Crawl4AiJobRegistry CreateRegistry()
      => new Crawl4AiJobRegistry(NullLogger<Crawl4AiJobRegistry>.Instance, TimeProvider.System);

    private static Crawl4AiCallbackHandler CreateHandler(Crawl4AiJobRegistry registry)
      => new Crawl4AiCallbackHandler(registry, NullLogger<Crawl4AiCallbackHandler>.Instance, ValidToken);

    private static string CompletedPayload(string taskId, string markdown)
      => $"{{\"task_id\":\"{taskId}\",\"status\":\"completed\",\"data\":{{\"markdown\":\"{markdown}\"}}}}";

    private static string CompletedResultsPayload(string taskId, string markdown)
      => $"{{\"task_id\":\"{taskId}\",\"status\":\"completed\",\"data\":{{\"results\":[{{\"markdown\":\"{markdown}\"}}]}}}}";

    private static string FailedPayload(string taskId, string error)
      => $"{{\"task_id\":\"{taskId}\",\"status\":\"failed\",\"error\":\"{error}\"}}";

    private static string CompletedNoMarkdownPayload(string taskId)
      => $"{{\"task_id\":\"{taskId}\",\"status\":\"completed\",\"data\":{{}}}}";

    [Fact]
    public async Task HandleAsync_BadSecret_Returns401AndRegistryUntouched()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      var task = registry.RegisterAndAwaitAsync("t1", TimeSpan.FromSeconds(30), CancellationToken.None);

      var result = await handler.HandleAsync("wrong-secret", CompletedPayload("t1", "hello"), "127.0.0.1");

      Assert.Equal(401, GetStatusCode(result));
      Assert.Equal(1, registry.ActiveJobCount);
    }

    [Fact]
    public async Task HandleAsync_NullSecret_Returns401()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);

      var result = await handler.HandleAsync(null, CompletedPayload("any", "md"), "127.0.0.1");

      Assert.Equal(401, GetStatusCode(result));
    }

    [Fact]
    public async Task HandleAsync_ValidSecret_StatusCompleted_DataMarkdown_CompletesRegistry()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      var task = registry.RegisterAndAwaitAsync("t2", TimeSpan.FromSeconds(30), CancellationToken.None);

      await handler.HandleAsync(ValidToken, CompletedPayload("t2", "hello md"), "127.0.0.1");

      var result = await task;
      Assert.Equal("hello md", result.Markdown);
    }

    [Fact]
    public async Task HandleAsync_ValidSecret_StatusCompleted_ResultsMarkdown_CompletesRegistry()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      var task = registry.RegisterAndAwaitAsync("t3", TimeSpan.FromSeconds(30), CancellationToken.None);

      await handler.HandleAsync(ValidToken, CompletedResultsPayload("t3", "results md"), "127.0.0.1");

      var result = await task;
      Assert.Equal("results md", result.Markdown);
    }

    [Fact]
    public async Task HandleAsync_ValidSecret_StatusCompleted_NoMarkdown_FailsRegistry()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      var task = registry.RegisterAndAwaitAsync("t4", TimeSpan.FromSeconds(30), CancellationToken.None);

      await handler.HandleAsync(ValidToken, CompletedNoMarkdownPayload("t4"), "127.0.0.1");

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("no markdown found", ex.Message);
      Assert.Contains("Preview:", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_ValidSecret_StatusFailed_FailsRegistryWithError()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      var task = registry.RegisterAndAwaitAsync("t5", TimeSpan.FromSeconds(30), CancellationToken.None);

      await handler.HandleAsync(ValidToken, FailedPayload("t5", "upstream failure"), "127.0.0.1");

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("upstream failure", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_ValidSecret_UnknownTaskId_StashesResult()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);

      await handler.HandleAsync(ValidToken, CompletedPayload("unknown-t", "stashed md"), "127.0.0.1");

      var result = await registry.RegisterAndAwaitAsync("unknown-t", TimeSpan.FromSeconds(5), CancellationToken.None);
      Assert.Equal("stashed md", result.Markdown);
    }

    [Fact]
    public async Task HandleAsync_CompletedNoMarkdown_FailReasonRedactsSecret()
    {
      var registry = CreateRegistry();
      var handler = CreateHandler(registry);
      string payload = $"{{\"task_id\":\"t-redact\",\"status\":\"completed\",\"data\":{{}},\"webhook_url\":\"http://cb/?secret=mysecret\"}}";
      var task = registry.RegisterAndAwaitAsync("t-redact", TimeSpan.FromSeconds(30), CancellationToken.None);

      await handler.HandleAsync(ValidToken, payload, "127.0.0.1");

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.DoesNotContain("mysecret", ex.Message);
      Assert.Contains("secret=REDACTED", ex.Message);
    }

    private static int GetStatusCode(IResult result)
    {
      var services = new ServiceCollection()
        .AddLogging()
        .BuildServiceProvider();
      var context = new DefaultHttpContext { RequestServices = services };
      context.Response.Body = System.IO.Stream.Null;
      result.ExecuteAsync(context).GetAwaiter().GetResult();
      return context.Response.StatusCode;
    }
  }
}
