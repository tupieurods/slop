using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class SearXngClientImageTests
  {
    private static SearXngClient CreateClient(HttpMessageHandler handler)
    {
      var httpClient = new HttpClient(handler)
      {
        BaseAddress = new Uri("http://searxng:8080/")
      };
      return new SearXngClient(httpClient, NullLogger<SearXngClient>.Instance);
    }

    [Fact]
    public async Task SearchImagesAsync_ParsesResultsCorrectly()
    {
      const string json = """
        {
          "results": [
            {
              "title": "Test Cat",
              "url": "https://example.com/page/cat",
              "img_src": "https://example.com/images/cat.jpg",
              "thumbnail_src": "https://example.com/thumbs/cat.jpg",
              "resolution": "1920x1080"
            },
            {
              "title": "Another Cat",
              "url": "https://other.com/cat",
              "img_src": "https://other.com/cat.png",
              "thumbnail_src": null,
              "resolution": null
            }
          ]
        }
        """;

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(json, Encoding.UTF8, "application/json")
        }
      );

      var client = CreateClient(handler);
      var results = await client.SearchImagesAsync("cat", 5);

      Assert.Equal(2, results.Count);

      Assert.Equal("Test Cat", results[0].Title);
      Assert.Equal("https://example.com/images/cat.jpg", results[0].ImageUrl);
      Assert.Equal("https://example.com/page/cat", results[0].SourceUrl);
      Assert.Equal("https://example.com/thumbs/cat.jpg", results[0].ThumbnailUrl);
      Assert.Equal("1920x1080", results[0].Resolution);

      Assert.Equal("Another Cat", results[1].Title);
      Assert.Equal("https://other.com/cat.png", results[1].ImageUrl);
      Assert.Equal("https://other.com/cat", results[1].SourceUrl);
      Assert.Null(results[1].ThumbnailUrl);
      Assert.Null(results[1].Resolution);
    }

    [Fact]
    public async Task SearchImagesAsync_SkipsResultsWithoutImgSrc()
    {
      const string json = """
        {
          "results": [
            {
              "title": "No Image",
              "url": "https://example.com/page",
              "img_src": null
            },
            {
              "title": "Has Image",
              "url": "https://example.com/page2",
              "img_src": "https://example.com/img.jpg"
            }
          ]
        }
        """;

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(json, Encoding.UTF8, "application/json")
        }
      );

      var client = CreateClient(handler);
      var results = await client.SearchImagesAsync("test", 5);

      Assert.Single(results);
      Assert.Equal("Has Image", results[0].Title);
    }

    [Fact]
    public async Task SearchImagesAsync_ReturnsEmptyOnHttpError()
    {
      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.InternalServerError)
      );

      var client = CreateClient(handler);
      var results = await client.SearchImagesAsync("test", 5);

      Assert.Empty(results);
    }

    [Fact]
    public async Task SearchImagesAsync_RespectsMaxResults()
    {
      const string json = """
        {
          "results": [
            { "title": "A", "url": "https://a.com", "img_src": "https://a.com/a.jpg" },
            { "title": "B", "url": "https://b.com", "img_src": "https://b.com/b.jpg" },
            { "title": "C", "url": "https://c.com", "img_src": "https://c.com/c.jpg" }
          ]
        }
        """;

      var handler = new FuncHttpMessageHandler(_ =>
        new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(json, Encoding.UTF8, "application/json")
        }
      );

      var client = CreateClient(handler);
      var results = await client.SearchImagesAsync("test", 2);

      Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchImagesAsync_UsesImagesCategoryAndSafesearchInUrl()
    {
      HttpRequestMessage? capturedRequest = null;
      var handler = new FuncHttpMessageHandler(req =>
      {
        capturedRequest = req;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
        };
      });

      var client = CreateClient(handler);
      await client.SearchImagesAsync("cats", 5);

      Assert.NotNull(capturedRequest);
      string fullUrl = capturedRequest!.RequestUri?.AbsoluteUri ?? "";
      Assert.Contains("categories=images", fullUrl);
      Assert.Contains("format=json", fullUrl);
      Assert.Contains("safesearch=1", fullUrl);
    }

    [Fact]
    public async Task SearchImagesAsync_EncodesQueryCorrectly()
    {
      HttpRequestMessage? capturedRequest = null;
      var handler = new FuncHttpMessageHandler(req =>
      {
        capturedRequest = req;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("{\"results\":[]}", Encoding.UTF8, "application/json")
        };
      });

      var client = CreateClient(handler);
      await client.SearchImagesAsync("cat dogs/&", 5);

      Assert.NotNull(capturedRequest);
      string fullUrl = capturedRequest!.RequestUri?.AbsoluteUri ?? "";
      Assert.Contains("cat%20dogs%2F%26", fullUrl);
    }
  }

  internal sealed class FuncHttpMessageHandler: HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FuncHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
      _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
      => Task.FromResult(_handler(request));
  }
}
