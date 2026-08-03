using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class SearXngClientSearchTests
  {
    private static SearXngClient CreateClient(HttpMessageHandler handler)
    {
      var httpClient = new HttpClient(handler)
      {
        BaseAddress = new Uri("http://searxng:8080/")
      };
      return new SearXngClient(httpClient, NullLogger<SearXngClient>.Instance);
    }

    private static StringContent JsonContent(string json)
      => new StringContent(json, Encoding.UTF8, "application/json");

    // ── SearchAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ParsesResultsAndUnresponsiveEngines()
    {
      const string json = """
        {
          "number_of_results": 1,
          "results": [
            { "title": "Test", "url": "https://example.com", "content": "snippet" }
          ],
          "unresponsive_engines": [["google","HTTP error 429"],["bing","timeout"]]
        }
        """;

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) }
      );

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Single(outcome.Results);
      Assert.Equal("Test", outcome.Results[0].Title);
      Assert.Equal(2, outcome.UnresponsiveEngines.Count);
      Assert.Equal("google", outcome.UnresponsiveEngines[0].Name);
      Assert.Equal("HTTP error 429", outcome.UnresponsiveEngines[0].Error);
      Assert.Equal("bing", outcome.UnresponsiveEngines[1].Name);
      Assert.Equal(HttpStatusCode.OK, outcome.HttpStatus);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SearchAsync_Http429_ReturnsStatusAndEmptyResults()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
          Content = JsonContent("{}")
        }
      );

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Equal(HttpStatusCode.TooManyRequests, outcome.HttpStatus);
      Assert.Empty(outcome.Results);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SearchAsync_Http500_ReturnsStatusAndEmptyResults()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
          Content = JsonContent("{}")
        }
      );

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Equal(HttpStatusCode.InternalServerError, outcome.HttpStatus);
      Assert.Empty(outcome.Results);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SearchAsync_Timeout_ReturnsTransportError()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        throw new TaskCanceledException("The request timed out.")
      );

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.NotNull(outcome.TransportError);
      Assert.Empty(outcome.Results);
      Assert.Null(outcome.HttpStatus);
    }

    [Fact]
    public async Task SearchAsync_EmptyResultsNoUnresponsiveEngines_NoFallbackCall()
    {
      const string json = """{"results":[],"unresponsive_engines":[]}""";
      int callCount = 0;

      var handler = new FuncHttpMessageHandler(_ =>
      {
        callCount++;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) };
      });

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Equal(1, callCount);
      Assert.Empty(outcome.Results);
      Assert.Empty(outcome.UnresponsiveEngines);
    }

    [Fact]
    public async Task SearchAsync_EmptyResultsWithUnresponsiveEngines_IssuesFallbackCall()
    {
      const string firstJson = """
        {
          "results": [],
          "unresponsive_engines": [["google","HTTP error 429"]]
        }
        """;
      const string secondJson = """
        {
          "results": [{"title":"Fallback","url":"https://duck.com","content":"found it"}],
          "unresponsive_engines": [["brave","timeout"]]
        }
        """;

      int callCount = 0;
      string? secondUrl = null;

      var handler = new FuncHttpMessageHandler(req =>
      {
        callCount++;
        if(callCount == 1)
        {
          return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(firstJson) };
        }
        secondUrl = req.RequestUri?.AbsoluteUri;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(secondJson) };
      });

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Equal(2, callCount);
      Assert.Contains("engines=", secondUrl);
      Assert.Single(outcome.Results);
      Assert.Equal("Fallback", outcome.Results[0].Title);
      Assert.Equal(2, outcome.UnresponsiveEngines.Count);
      Assert.Contains(outcome.UnresponsiveEngines, e => e.Name == "google");
      Assert.Contains(outcome.UnresponsiveEngines, e => e.Name == "brave");
    }

    [Fact]
    public async Task SearchAsync_UnresponsiveEnginesMergedDeduplicatedByName()
    {
      const string firstJson = """
        {"results":[],"unresponsive_engines":[["google","error"],["bing","error"]]}
        """;
      const string secondJson = """
        {"results":[],"unresponsive_engines":[["google","error2"],["qwant","error"]]}
        """;

      int callCount = 0;
      var handler = new FuncHttpMessageHandler(_ =>
      {
        callCount++;
        string body = callCount == 1 ? firstJson : secondJson;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(body) };
      });

      var client = CreateClient(handler);
      var outcome = await client.SearchAsync("test", 5);

      Assert.Equal(2, callCount);
      var names = outcome.UnresponsiveEngines.Select(e => e.Name).ToList();
      Assert.Equal(names.Distinct().Count(), names.Count);
      Assert.Contains("google", names);
      Assert.Contains("bing", names);
      Assert.Contains("qwant", names);
    }

    // ── SearchImagesAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SearchImagesAsync_ParsesResultsAndUnresponsiveEngines()
    {
      const string json = """
        {
          "results": [
            {
              "title": "Cat",
              "url": "https://example.com",
              "img_src": "https://example.com/cat.jpg",
              "thumbnail_src": "https://example.com/cat_thumb.jpg",
              "resolution": "800x600"
            }
          ],
          "unresponsive_engines": [["google images","bot detected"]]
        }
        """;

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(json) }
      );

      var client = CreateClient(handler);
      var outcome = await client.SearchImagesAsync("cat", 5);

      Assert.Single(outcome.Results);
      Assert.Equal("Cat", outcome.Results[0].Title);
      Assert.Equal(HttpStatusCode.OK, outcome.HttpStatus);
      Assert.Single(outcome.UnresponsiveEngines);
      Assert.Equal("google images", outcome.UnresponsiveEngines[0].Name);
      Assert.Null(outcome.TransportError);
    }

    [Fact]
    public async Task SearchImagesAsync_EmptyResultsWithUnresponsiveEngines_IssuesFallbackCall()
    {
      const string firstJson = """
        {"results":[],"unresponsive_engines":[["google images","429"]]}
        """;
      const string secondJson = """
        {
          "results": [{"title":"Duck","url":"https://d.com","img_src":"https://d.com/img.jpg"}],
          "unresponsive_engines": []
        }
        """;

      int callCount = 0;
      string? secondUrl = null;

      var handler = new FuncHttpMessageHandler(req =>
      {
        callCount++;
        if(callCount == 1)
        {
          return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(firstJson) };
        }
        secondUrl = req.RequestUri?.AbsoluteUri;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent(secondJson) };
      });

      var client = CreateClient(handler);
      var outcome = await client.SearchImagesAsync("cat", 5);

      Assert.Equal(2, callCount);
      Assert.Contains("engines=duckduckgo%20images,brave.images,qwant%20images", secondUrl);
      Assert.Single(outcome.Results);
      Assert.Equal("Duck", outcome.Results[0].Title);
    }
  }
}
