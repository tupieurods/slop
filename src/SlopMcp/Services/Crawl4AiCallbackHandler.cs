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

          if(root.TryGetProperty("data", out var data))
          {
            if(data.TryGetProperty("results", out var results) &&
               results.ValueKind == JsonValueKind.Array &&
               results.GetArrayLength() > 0 &&
               results[0].TryGetProperty("markdown", out var mdProp))
            {
              markdown = mdProp.GetString();
            }
            else if(data.TryGetProperty("markdown", out var mdDirect))
            {
              markdown = mdDirect.GetString();
            }
          }

          if(!string.IsNullOrEmpty(markdown))
          {
            _registry.Complete(taskId, markdown);
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
