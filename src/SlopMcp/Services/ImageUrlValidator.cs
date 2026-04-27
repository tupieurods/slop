using System.Net;
using System.Net.Sockets;

namespace SlopMcp.Services {

  public class ImageUrlValidator
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImageUrlValidator> _logger;
    private readonly TimeSpan _perUrlTimeout;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _dnsResolver;

    private const int ConcurrencyLimit = 4;

    public ImageUrlValidator(
      HttpClient httpClient,
      ILogger<ImageUrlValidator> logger,
      TimeSpan? perUrlTimeout = null,
      Func<string, CancellationToken, Task<IPAddress[]>>? dnsResolver = null
    )
    {
      _httpClient = httpClient;
      _logger = logger;
      _perUrlTimeout = perUrlTimeout ?? TimeSpan.FromSeconds(3);
      _dnsResolver = dnsResolver ?? ((host, ct) => Dns.GetHostAddressesAsync(host, ct));
    }

    public async Task<IReadOnlyList<string>> ValidateAsync(IEnumerable<string> urls, CancellationToken ct)
    {
      var urlList = urls.ToList();
      var semaphore = new SemaphoreSlim(ConcurrencyLimit);
      var tasks = urlList.Select(url => ValidateUrlAsync(url, semaphore, ct)).ToList();
      bool[] results = await Task.WhenAll(tasks);

      var validated = new List<string>();
      for(int i = 0; i < urlList.Count; i++)
      {
        if(results[i])
        {
          validated.Add(urlList[i]);
        }
      }

      return validated;
    }

    private async Task<bool> ValidateUrlAsync(string url, SemaphoreSlim semaphore, CancellationToken ct)
    {
      await semaphore.WaitAsync(ct);
      try
      {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_perUrlTimeout);

        try
        {
          return await TryValidateAsync(url, cts.Token);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested)
        {
          throw;
        }
        catch(OperationCanceledException)
        {
          _logger.LogDebug("URL validation timed out: {Url}", url);
          return false;
        }
        catch(Exception ex)
        {
          _logger.LogDebug(ex, "URL validation failed: {Url}", url);
          return false;
        }
      }
      finally
      {
        semaphore.Release();
      }
    }

    private async Task<bool> TryValidateAsync(string url, CancellationToken ct)
    {
      if(!await IsUrlSafeAsync(url, ct))
      {
        return false;
      }

      using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
      HttpResponseMessage headResponse;

      try
      {
        headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, ct);
      }
      catch(OperationCanceledException)
      {
        throw;
      }
      catch(Exception ex)
      {
        _logger.LogDebug(ex, "HEAD request failed for URL: {Url}", url);
        return false;
      }

      using(headResponse)
      {
        if(!headResponse.IsSuccessStatusCode)
        {
          return await TryRangedGetAsync(url, ct);
        }

        string? mediaType = headResponse.Content.Headers.ContentType?.MediaType;
        if(string.IsNullOrEmpty(mediaType))
        {
          return await TryRangedGetAsync(url, ct);
        }

        return IsImageContentType(mediaType);
      }
    }

    private async Task<bool> TryRangedGetAsync(string url, CancellationToken ct)
    {
      using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
      getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);

      try
      {
        using var getResponse = await _httpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, ct);

        if(!getResponse.IsSuccessStatusCode)
        {
          _logger.LogDebug("Ranged GET returned {StatusCode}: {Url}", getResponse.StatusCode, url);
          return false;
        }

        return IsImageContentType(getResponse.Content.Headers.ContentType?.MediaType);
      }
      catch(OperationCanceledException)
      {
        throw;
      }
      catch(Exception ex)
      {
        _logger.LogDebug(ex, "Ranged GET failed for URL: {Url}", url);
        return false;
      }
    }

    private async Task<bool> IsUrlSafeAsync(string url, CancellationToken ct)
    {
      if(!Uri.TryCreate(url, UriKind.Absolute, out var uri))
      {
        _logger.LogDebug("Rejected invalid URL: {Url}", url);
        return false;
      }

      if(uri.Scheme != "http" && uri.Scheme != "https")
      {
        _logger.LogDebug("Rejected non-HTTP/HTTPS scheme: {Url}", url);
        return false;
      }

      if(uri.HostNameType == UriHostNameType.IPv4 || uri.HostNameType == UriHostNameType.IPv6)
      {
        if(!IPAddress.TryParse(uri.DnsSafeHost, out var ip))
        {
          _logger.LogDebug("Rejected URL with unparsable IP: {Url}", url);
          return false;
        }

        if(IsPrivateOrRestrictedAddress(ip))
        {
          _logger.LogDebug("Rejected URL with private/loopback IP: {Url}", url);
          return false;
        }

        return true;
      }

      try
      {
        IPAddress[] addresses = await _dnsResolver(uri.DnsSafeHost, ct);
        foreach(var addr in addresses)
        {
          if(IsPrivateOrRestrictedAddress(addr))
          {
            _logger.LogDebug("Rejected URL: DNS resolved to private/loopback address {Address}: {Url}", addr, url);
            return false;
          }
        }
      }
      catch(OperationCanceledException)
      {
        throw;
      }
      catch(Exception ex)
      {
        _logger.LogDebug(ex, "DNS resolution failed for URL: {Url}", url);
        return false;
      }

      return true;
    }

    private static bool IsPrivateOrRestrictedAddress(IPAddress ip)
    {
      if(ip.IsIPv4MappedToIPv6)
      {
        ip = ip.MapToIPv4();
      }

      if(IPAddress.IsLoopback(ip))
      {
        return true;
      }

      if(ip.AddressFamily == AddressFamily.InterNetwork)
      {
        return IsPrivateIPv4(ip);
      }

      if(ip.AddressFamily == AddressFamily.InterNetworkV6)
      {
        return IsPrivateIPv6(ip);
      }

      return false;
    }

    private static bool IsPrivateIPv4(IPAddress ip)
    {
      byte[] bytes = ip.GetAddressBytes();
      return bytes[0] == 10
        || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
        || (bytes[0] == 192 && bytes[1] == 168)
        || (bytes[0] == 169 && bytes[1] == 254);
    }

    private static bool IsPrivateIPv6(IPAddress ip)
    {
      byte[] bytes = ip.GetAddressBytes();
      if((bytes[0] & 0xFE) == 0xFC)
      {
        return true;
      }

      if(bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
      {
        return true;
      }

      return false;
    }

    private static bool IsImageContentType(string? contentType)
      => contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false;
  }

}
