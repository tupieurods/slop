using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlopMcp.Services {

  public record Crawl4AiJobSubmitOutcome
  {
    public string? TaskId { get; init; }
    public System.Net.HttpStatusCode? HttpStatus { get; init; }
    public string? TransportError { get; init; }
    public string? ResponseBodyPreview { get; init; }
  }

  public class Crawl4AiClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<Crawl4AiClient> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      PropertyNameCaseInsensitive = true
    };

    public Crawl4AiClient(HttpClient httpClient, ILogger<Crawl4AiClient> logger, TimeProvider timeProvider)
    {
      _httpClient = httpClient;
      _logger = logger;
      _timeProvider = timeProvider;
    }

    internal static string RedactSecretFromJson(string json)
      => SecretRedactor.Redact(json);

    public async Task<Crawl4AiJobSubmitOutcome> SubmitCrawlJobAsync(
      string url,
      string callbackUrl,
      CancellationToken ct = default
    )
    {
      string correlationId = Guid.NewGuid().ToString("N")[..8];
      _logger.LogInformation(
        "Crawl4AI submit start [{CorrelationId}]: url={Url}, callback={Callback}",
        correlationId, url, callbackUrl
      );

      long startTs = _timeProvider.GetTimestamp();

      var body = new
      {
        urls = new[] { url },
        crawler_config = new
        {
          type = "CrawlerRunConfig",
          @params = new { cache_mode = "bypass" }
        },
        // Using ?secret=<token> on the callback URL for validation because the
        // crawl4ai webhook_config schema does not guarantee support for arbitrary
        // extra_headers — only webhook_url and webhook_data_in_payload are
        // documented. The secret is embedded in callbackUrl by the caller.
        webhook_config = new
        {
          webhook_url = callbackUrl,
          webhook_data_in_payload = true
        }
      };

      string json = JsonSerializer.Serialize(body, _jsonOptions);
      _logger.LogDebug(
        "Crawl4AI submit request [{CorrelationId}]: body={Json}",
        correlationId, RedactSecretFromJson(json)
      );

      using var content = new StringContent(json, Encoding.UTF8, "application/json");

      try
      {
        using HttpResponseMessage response = await _httpClient.PostAsync("crawl/job", content, ct);
        string responseBody = await response.Content.ReadAsStringAsync(ct);

        if(!response.IsSuccessStatusCode)
        {
          string preview = responseBody.Length > 500 ? responseBody[..500] : responseBody;
          preview = SecretRedactor.Redact(preview);
          _logger.LogError(
            "Crawl4AI job submit returned non-2xx status {Status} [{CorrelationId}]. Body: {Preview}",
            (int)response.StatusCode, correlationId, preview
          );
          return new Crawl4AiJobSubmitOutcome
          {
            HttpStatus = response.StatusCode,
            ResponseBodyPreview = preview
          };
        }

        var parsed = JsonSerializer.Deserialize<SubmitResponse>(responseBody, _jsonOptions);
        string? taskId = parsed?.TaskId;

        long elapsedMs = (long)_timeProvider.GetElapsedTime(startTs).TotalMilliseconds;
        _logger.LogInformation(
          "Crawl4AI job submitted successfully [{CorrelationId}]: task_id={TaskId}, elapsed={ElapsedMs}ms",
          correlationId, taskId, elapsedMs
        );

        return new Crawl4AiJobSubmitOutcome
        {
          TaskId = taskId,
          HttpStatus = response.StatusCode
        };
      }
      catch(HttpRequestException ex)
      {
        _logger.LogError(ex, "Crawl4AI job submit HTTP error [{CorrelationId}]: url={Url}", correlationId, url);
        return new Crawl4AiJobSubmitOutcome { TransportError = ex.Message };
      }
      catch(TaskCanceledException ex)
      {
        _logger.LogError(ex, "Crawl4AI job submit timed out [{CorrelationId}]: url={Url}", correlationId, url);
        return new Crawl4AiJobSubmitOutcome { TransportError = ex.Message };
      }
      catch(JsonException ex)
      {
        _logger.LogError(ex, "Crawl4AI job submit response parse failed [{CorrelationId}]: url={Url}", correlationId, url);
        return new Crawl4AiJobSubmitOutcome { TransportError = ex.Message };
      }
    }

    private class SubmitResponse
    {
      [JsonPropertyName("task_id")]
      public string? TaskId { get; set; }
    }
  }

}
