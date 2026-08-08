using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SlopMcp.Services {

  public class Crawl4AiCallbackHandler
  {
    private readonly Crawl4AiJobRegistry _registry;
    private readonly ILogger<Crawl4AiCallbackHandler> _logger;
    private readonly string _expectedToken;

    public Crawl4AiCallbackHandler(
      Crawl4AiJobRegistry registry,
      ILogger<Crawl4AiCallbackHandler> logger,
      string expectedToken
    )
    {
      _registry = registry;
      _logger = logger;
      _expectedToken = expectedToken;
    }

    public async Task<IResult> HandleAsync(string? secret, string body, string sourceIp)
    {
      if(!IsSecretValid(secret))
      {
        _logger.LogWarning(
          "Crawl4AI callback rejected: invalid secret from {Ip}, content-length={Length}",
          sourceIp, body.Length
        );
        return Results.StatusCode(401);
      }

      _logger.LogInformation("Crawl4AI callback received: payloadSize={Size}", body.Length);
      if(_logger.IsEnabled(LogLevel.Debug))
      {
        string full = body.Length > 4000 ? body[..4000] + "…[truncated]" : body;
        _logger.LogDebug("Crawl4AI callback raw body (redacted): {Body}", SecretRedactor.Redact(full));
      }

      JsonDocument doc;
      try
      {
        doc = JsonDocument.Parse(body);
      }
      catch(JsonException ex)
      {
        _logger.LogError(ex, "Crawl4AI callback body parse failed");
        return Results.Ok();
      }

      using(doc)
      {
        var root = doc.RootElement;
        string? taskId = root.TryGetProperty("task_id", out var tid) ? tid.GetString() : null;
        string? status = root.TryGetProperty("status", out var st) ? st.GetString() : null;

        _logger.LogInformation(
          "Crawl4AI callback: task_id={TaskId}, status={Status}",
          taskId, status
        );

        if(string.IsNullOrEmpty(taskId))
        {
          _logger.LogWarning("Crawl4AI callback missing task_id");
          return Results.Ok();
        }

        if(status == "completed")
        {
          string? markdown = null;
          string? innerError = null;
          bool innerSuccess = true;

          if(root.TryGetProperty("data", out var data))
          {
            if(data.TryGetProperty("success", out var outerSuccess) &&
               outerSuccess.ValueKind == JsonValueKind.False)
            {
              innerSuccess = false;
            }

            JsonElement? firstResult = null;
            if(data.TryGetProperty("results", out var results) &&
               results.ValueKind == JsonValueKind.Array &&
               results.GetArrayLength() > 0)
            {
              firstResult = results[0];
            }

            if(firstResult is JsonElement r)
            {
              if(r.TryGetProperty("success", out var rSuccess) &&
                 rSuccess.ValueKind == JsonValueKind.False)
              {
                innerSuccess = false;
              }
              if(r.TryGetProperty("error_message", out var rErr) &&
                 rErr.ValueKind == JsonValueKind.String)
              {
                innerError = rErr.GetString();
              }
              if(r.TryGetProperty("markdown", out var mdProp))
              {
                markdown = ExtractMarkdown(mdProp);
              }
            }

            if(string.IsNullOrEmpty(markdown) &&
               data.TryGetProperty("markdown", out var mdDirect))
            {
              markdown = ExtractMarkdown(mdDirect);
            }
          }

          if(!string.IsNullOrEmpty(markdown) && innerSuccess)
          {
            _registry.Complete(taskId, markdown);
          }
          else if(!innerSuccess || !string.IsNullOrEmpty(innerError))
          {
            string reason = string.IsNullOrEmpty(innerError)
              ? "crawl4ai reported the crawl failed but did not include an error message"
              : $"crawl4ai could not fetch the page: {innerError}";
            _logger.LogWarning(
              "Crawl4AI task {TaskId} failed inside crawler: {Reason}",
              taskId, reason
            );
            _registry.Fail(taskId, reason);
          }
          else
          {
            string preview = body.Length > 500 ? body[..500] : body;
            preview = SecretRedactor.Redact(preview);
            _registry.Fail(taskId, $"crawl4ai returned status=completed but no markdown found in payload. Preview: {preview}");
          }
        }
        else
        {
          string? error = root.TryGetProperty("error", out var err) ? err.GetString() : null;
          _registry.Fail(taskId, error ?? "crawl4ai returned failure status");
        }
      }

      return Results.Ok();
    }

    private static string? ExtractMarkdown(JsonElement mdProp)
    {
      if(mdProp.ValueKind == JsonValueKind.String)
      {
        return mdProp.GetString();
      }
      if(mdProp.ValueKind == JsonValueKind.Object)
      {
        foreach(string key in new[] { "fit_markdown", "raw_markdown", "markdown_with_citations" })
        {
          if(mdProp.TryGetProperty(key, out var inner) &&
             inner.ValueKind == JsonValueKind.String)
          {
            string? s = inner.GetString();
            if(!string.IsNullOrEmpty(s))
            {
              return s;
            }
          }
        }
      }
      return null;
    }

    private bool IsSecretValid(string? secret)
    {
      if(secret is null)
      {
        return false;
      }

      byte[] expectedBytes = Encoding.UTF8.GetBytes(_expectedToken);
      byte[] actualBytes = Encoding.UTF8.GetBytes(secret);

      if(expectedBytes.Length != actualBytes.Length)
      {
        return false;
      }

      return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
  }

}
