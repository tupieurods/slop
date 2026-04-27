using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class ImageUrlValidatorTests
  {
    private static ImageUrlValidator CreateValidator(
      HttpMessageHandler handler,
      TimeSpan? timeout = null,
      Func<string, CancellationToken, Task<System.Net.IPAddress[]>>? dnsResolver = null
    )
    {
      var httpClient = new HttpClient(handler);
      return new ImageUrlValidator(httpClient, NullLogger<ImageUrlValidator>.Instance, timeout, dnsResolver);
    }

    [Fact]
    public async Task Validate_200WithImageContentType_IsKept()
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.jpg"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.jpg", result[0]);
    }

    [Fact]
    public async Task Validate_404_IsDropped()
    {
      var handler = new FuncHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/missing.jpg"], CancellationToken.None);

      Assert.Empty(result);
    }

    [Fact]
    public async Task Validate_200WithTextHtml_IsDropped()
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/page.html"], CancellationToken.None);

      Assert.Empty(result);
    }

    [Fact]
    public async Task Validate_405OnHeadThen206OnRangedGet_IsKept()
    {
      var handler = new FuncHttpMessageHandler(req =>
      {
        if(req.Method == HttpMethod.Head)
        {
          return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.png"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.png", result[0]);
    }

    [Fact]
    public async Task Validate_Timeout_IsDropped()
    {
      var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(10));
      var validator = CreateValidator(handler, timeout: TimeSpan.FromMilliseconds(50), dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/slow.jpg"], CancellationToken.None);

      Assert.Empty(result);
    }

    [Fact]
    public async Task Validate_MultipleUrls_ValidatedInParallel()
    {
      var handler = new FuncHttpMessageHandler(req =>
      {
        bool isImage = req.RequestUri?.AbsolutePath.EndsWith(".jpg") == true;
        var response = new HttpResponseMessage(isImage ? HttpStatusCode.OK : HttpStatusCode.NotFound);
        if(isImage)
        {
          response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        }
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(
        [
          "https://example.com/a.jpg",
          "https://example.com/b.html",
          "https://example.com/c.jpg"
        ],
        CancellationToken.None
      );

      Assert.Equal(2, result.Count);
      Assert.Contains("https://example.com/a.jpg", result);
      Assert.Contains("https://example.com/c.jpg", result);
    }

    [Theory]
    [InlineData("http://127.0.0.1/x.jpg")]
    [InlineData("http://10.0.0.1/x.jpg")]
    [InlineData("http://169.254.169.254/x.jpg")]
    [InlineData("http://192.168.1.1/x.jpg")]
    [InlineData("http://172.16.0.1/x.jpg")]
    [InlineData("http://[::ffff:127.0.0.1]/x.jpg")]
    [InlineData("http://[::ffff:10.0.0.1]/x.jpg")]
    [InlineData("http://[::1]/x.jpg")]
    [InlineData("http://[fc00::1]/x.jpg")]
    [InlineData("http://[fe80::1]/x.jpg")]
    public async Task Validate_PrivateOrLoopbackIP_IsDropped(string url)
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
      });

      var validator = CreateValidator(handler);
      var result = await validator.ValidateAsync([url], CancellationToken.None);

      Assert.Empty(result);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/resource")]
    [InlineData("ftp://example.com/image.jpg")]
    public async Task Validate_NonHttpScheme_IsDropped(string url)
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
      });

      var validator = CreateValidator(handler);
      var result = await validator.ValidateAsync([url], CancellationToken.None);

      Assert.Empty(result);
    }

    [Fact]
    public async Task Validate_DnsResolvesToPrivateIP_IsDropped()
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
      });

      Task<System.Net.IPAddress[]> PrivateDns(string host, CancellationToken ct)
        => Task.FromResult<System.Net.IPAddress[]>([System.Net.IPAddress.Parse("10.0.0.1")]);

      var validator = CreateValidator(handler, dnsResolver: PrivateDns);
      var result = await validator.ValidateAsync(["https://internal.example.com/image.jpg"], CancellationToken.None);

      Assert.Empty(result);
    }

    [Fact]
    public async Task Validate_HeadReturnsNoContentType_FallsThroughToRangedGet_IsKept()
    {
      var handler = new FuncHttpMessageHandler(req =>
      {
        if(req.Method == HttpMethod.Head)
        {
          return new HttpResponseMessage(HttpStatusCode.OK);
        }

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.png"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.png", result[0]);
    }

    [Fact]
    public async Task Validate_403OnHeadThen206OnRangedGet_IsKept()
    {
      var handler = new FuncHttpMessageHandler(req =>
      {
        if(req.Method == HttpMethod.Head)
        {
          return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.png"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.png", result[0]);
    }

    [Fact]
    public async Task Validate_501OnHeadThen206OnRangedGet_IsKept()
    {
      var handler = new FuncHttpMessageHandler(req =>
      {
        if(req.Method == HttpMethod.Head)
        {
          return new HttpResponseMessage(HttpStatusCode.NotImplemented);
        }

        var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.jpg"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.jpg", result[0]);
    }

    [Fact]
    public async Task Validate_ContentTypeWithParameters_IsKept()
    {
      var handler = new FuncHttpMessageHandler(_ =>
      {
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg; charset=binary");
        return response;
      });

      var validator = CreateValidator(handler, dnsResolver: StubPublicDns);
      var result = await validator.ValidateAsync(["https://example.com/image.jpg"], CancellationToken.None);

      Assert.Single(result);
      Assert.Equal("https://example.com/image.jpg", result[0]);
    }

    [Fact]
    public async Task ValidateAsync_OuterCancellation_Propagates()
    {
      var handler = new DelayedHttpMessageHandler(TimeSpan.FromSeconds(10));
      var validator = CreateValidator(
        handler,
        timeout: TimeSpan.FromSeconds(10),
        dnsResolver: (_, _) => Task.FromResult<System.Net.IPAddress[]>([System.Net.IPAddress.Parse("93.184.216.34")])
      );

      using var cts = new CancellationTokenSource();
      var task = validator.ValidateAsync(["https://example.com/image.jpg"], cts.Token);
      cts.CancelAfter(TimeSpan.FromMilliseconds(50));

      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    private static Task<System.Net.IPAddress[]> StubPublicDns(string host, CancellationToken ct)
      => Task.FromResult<System.Net.IPAddress[]>([System.Net.IPAddress.Parse("93.184.216.34")]);
  }

  internal sealed class DelayedHttpMessageHandler: HttpMessageHandler
  {
    private readonly TimeSpan _delay;

    public DelayedHttpMessageHandler(TimeSpan delay)
    {
      _delay = delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      await Task.Delay(_delay, cancellationToken);
      return new HttpResponseMessage(HttpStatusCode.OK);
    }
  }
}
