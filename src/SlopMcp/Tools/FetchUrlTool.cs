using System.Collections.Frozen;
using System.ComponentModel;
using System.Net;
using ModelContextProtocol.Server;
using SlopMcp.Configuration;
using SlopMcp.Services;

namespace SlopMcp.Tools {

  public class FetchUrlTool
  {
    private static readonly FrozenSet<string> _internalHostNames =
      new[] { "crawl4ai", "slopmcp", "searxng", "slopchat", "localhost" }
        .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly Crawl4AiClient _client;
    private readonly Crawl4AiJobRegistry _registry;
    private readonly Crawl4AiOptions _options;
    private readonly ILogger<FetchUrlTool> _logger;

    public FetchUrlTool(
      Crawl4AiClient client,
      Crawl4AiJobRegistry registry,
      Crawl4AiOptions options,
      ILogger<FetchUrlTool> logger
    )
    {
      _client = client;
      _registry = registry;
      _options = options;
      _logger = logger;
    }

    [McpServerTool(Name = "fetch_url"), Description(
      "Fetch the contents of a specific URL as markdown. Use this whenever the user provides a link " +
      "and wants you to read it, or when a promising URL from web_search needs to be read in full."
    )]
    public async Task<string> FetchAsync(
      [Description("The URL to fetch")] string url,
      CancellationToken ct = default
    )
    {
      if(!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
         (uri.Scheme != "http" && uri.Scheme != "https"))
      {
        return $"Invalid URL: '{url}'. Only http and https URLs are supported.";
      }

      if(IsInternalHost(uri, out string blockReason))
      {
        return $"Refusing to fetch internal or loopback address: {blockReason}";
      }

      string callbackUrl = $"{_options.CallbackUrl}?secret={Uri.EscapeDataString(_options.Token)}";
      var outcome = await _client.SubmitCrawlJobAsync(url, callbackUrl, ct);

      if(outcome.TransportError is not null)
      {
        return $"Fetch backend error: {outcome.TransportError}. Try again later.";
      }

      if(outcome.HttpStatus is not null && ((int)outcome.HttpStatus.Value < 200 || (int)outcome.HttpStatus.Value >= 300))
      {
        return $"Fetch backend error (HTTP {(int)outcome.HttpStatus.Value}). Try again later.";
      }

      if(string.IsNullOrEmpty(outcome.TaskId))
      {
        return "Fetch backend error: no task_id returned. Try again later.";
      }

      string taskId = outcome.TaskId;

      try
      {
        var result = await _registry.RegisterAndAwaitAsync(
          taskId,
          TimeSpan.FromSeconds(_options.TimeoutSeconds),
          ct
        );

        string markdown = result.Markdown;
        const int maxLength = 15_000;

        if(markdown.Length > maxLength)
        {
          int original = markdown.Length;
          markdown = markdown[..maxLength] + $"\n\n...[truncated, original length: {original} chars]";
        }

        return $"# {url}\n\n{markdown}";
      }
      catch(OperationCanceledException)
      {
        _logger.LogWarning("Crawl4AI fetch cancelled or timed out: task_id={TaskId}, url={Url}", taskId, url);
        return $"Fetch cancelled or timed out after {_options.TimeoutSeconds} seconds.";
      }
      catch(InvalidOperationException ex)
      {
        return $"Fetch failed: {ex.Message}";
      }
    }

    internal static bool IsInternalHost(Uri uri, out string reason)
    {
      string host = uri.Host.TrimEnd('.').ToLowerInvariant();

      if(_internalHostNames.Contains(host))
      {
        reason = host;
        return true;
      }

      if(IPAddress.TryParse(host, out var ip))
      {
        if(ip.IsIPv4MappedToIPv6)
        {
          ip = ip.MapToIPv4();
        }

        if(IPAddress.IsLoopback(ip) ||
           ip.Equals(IPAddress.Any) ||
           ip.Equals(IPAddress.IPv6Any))
        {
          reason = host;
          return true;
        }

        if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
          byte[] b = ip.GetAddressBytes();

          if(b[0] == 10 ||
             (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
             (b[0] == 192 && b[1] == 168) ||
             (b[0] == 169 && b[1] == 254))
          {
            reason = host;
            return true;
          }
        }

        if(ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
          byte[] b = ip.GetAddressBytes();

          bool isFc00 = (b[0] & 0xFE) == 0xFC;
          bool isFe80 = b[0] == 0xFE && (b[1] & 0xC0) == 0x80;

          if(isFc00 || isFe80)
          {
            reason = host;
            return true;
          }
        }
      }

      reason = string.Empty;
      return false;
    }
  }

}
