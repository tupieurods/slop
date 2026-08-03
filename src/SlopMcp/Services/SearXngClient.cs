using System.Text.Json;
using System.Text.Json.Serialization;
using SlopMcp.Models;

namespace SlopMcp.Services {

  public class SearXngClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearXngClient> _logger;

    private static readonly string[] _textFallbackEngines =
    [
      "duckduckgo", "brave", "qwant", "wikipedia", "wikidata", "mojeek", "startpage"
    ];

    private static readonly string[] _imageFallbackEngines =
    [
      "duckduckgo images", "brave.images", "qwant images"
    ];

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
      PropertyNameCaseInsensitive = true
    };

    public SearXngClient(HttpClient httpClient, ILogger<SearXngClient> logger)
    {
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<SearXngSearchOutcome<SearchResult>> SearchAsync(
      string query,
      int maxResults = 5,
      CancellationToken ct = default
    )
    {
      string encodedQuery = Uri.EscapeDataString(query);
      string url = $"search?q={encodedQuery}&format=json";

      var first = await ExecuteSearchAsync(url, query, maxResults, ct);

      if(ShouldFallback(first))
      {
        string fallbackEngines = string.Join(",", _textFallbackEngines.Select(Uri.EscapeDataString));
        _logger.LogWarning(
          "SearXNG primary engines returned no results (unresponsive: {Engines}); retrying with fallback set",
          string.Join(", ", first.UnresponsiveEngines.Select(e => e.Name))
        );
        var second = await ExecuteSearchAsync($"{url}&engines={fallbackEngines}", query, maxResults, ct);
        return MergeOutcomes(first, second);
      }

      return first;
    }

    public async Task<SearXngSearchOutcome<SearXngImageResult>> SearchImagesAsync(
      string query,
      int maxResults = 5,
      CancellationToken ct = default
    )
    {
      string encodedQuery = Uri.EscapeDataString(query);
      string url = $"search?q={encodedQuery}&categories=images&format=json&safesearch=1";

      var first = await ExecuteImageSearchAsync(url, query, maxResults, ct);

      if(ShouldFallback(first))
      {
        string fallbackEngines = string.Join(",", _imageFallbackEngines.Select(Uri.EscapeDataString));
        _logger.LogWarning(
          "SearXNG primary engines returned no results (unresponsive: {Engines}); retrying with fallback set",
          string.Join(", ", first.UnresponsiveEngines.Select(e => e.Name))
        );
        var second = await ExecuteImageSearchAsync($"{url}&engines={fallbackEngines}", query, maxResults, ct);
        return MergeOutcomes(first, second);
      }

      return first;
    }

    private async Task<SearXngSearchOutcome<SearchResult>> ExecuteSearchAsync(
      string url,
      string query,
      int maxResults,
      CancellationToken ct
    )
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

        if(!response.IsSuccessStatusCode)
        {
          string preview = json.Length > 500 ? json[..500] : json;
          _logger.LogError(
            "SearXNG search returned non-2xx status {StatusCode} for query {Query}. Body: {Preview}",
            (int)response.StatusCode, query, preview
          );
          return new SearXngSearchOutcome<SearchResult> { HttpStatus = response.StatusCode };
        }

        SearXngResponse? parsed = JsonSerializer.Deserialize<SearXngResponse>(json, _jsonOptions);
        var engines = parsed?.UnresponsiveEngines ?? [];

        if(engines.Count > 0)
        {
          _logger.LogWarning(
            "SearXNG search completed with unresponsive engines: {Engines}",
            string.Join(", ", engines.Select(e => e.Name))
          );
        }

        var results = parsed?.Results is null
          ? (IReadOnlyList<SearchResult>)[]
          : parsed.Results
              .Take(maxResults)
              .Select(r => new SearchResult
              {
                Title = r.Title ?? string.Empty,
                Url = r.Url ?? string.Empty,
                Content = r.Content ?? string.Empty
              })
              .ToList();

        return new SearXngSearchOutcome<SearchResult>
        {
          Results = results,
          HttpStatus = response.StatusCode,
          UnresponsiveEngines = engines
        };
      }
      catch(HttpRequestException ex)
      {
        _logger.LogError(ex, "SearXNG search HTTP request failed for query: {Query}", query);
        return new SearXngSearchOutcome<SearchResult> { TransportError = ex.Message };
      }
      catch(TaskCanceledException ex)
      {
        _logger.LogError(ex, "SearXNG search timed out for query: {Query}", query);
        return new SearXngSearchOutcome<SearchResult> { TransportError = ex.Message };
      }
      catch(JsonException ex)
      {
        _logger.LogError(ex, "SearXNG search response parse failed for query: {Query}", query);
        return new SearXngSearchOutcome<SearchResult> { TransportError = ex.Message };
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "SearXNG search failed unexpectedly for query: {Query}", query);
        return new SearXngSearchOutcome<SearchResult> { TransportError = ex.Message };
      }
    }

    private async Task<SearXngSearchOutcome<SearXngImageResult>> ExecuteImageSearchAsync(
      string url,
      string query,
      int maxResults,
      CancellationToken ct
    )
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        string json = await response.Content.ReadAsStringAsync(ct);

        if(!response.IsSuccessStatusCode)
        {
          string preview = json.Length > 500 ? json[..500] : json;
          _logger.LogError(
            "SearXNG image search returned non-2xx status {StatusCode} for query {Query}. Body: {Preview}",
            (int)response.StatusCode, query, preview
          );
          return new SearXngSearchOutcome<SearXngImageResult> { HttpStatus = response.StatusCode };
        }

        SearXngImageResponse? parsed = JsonSerializer.Deserialize<SearXngImageResponse>(json, _jsonOptions);
        var engines = parsed?.UnresponsiveEngines ?? [];

        if(engines.Count > 0)
        {
          _logger.LogWarning(
            "SearXNG search completed with unresponsive engines: {Engines}",
            string.Join(", ", engines.Select(e => e.Name))
          );
        }

        var results = parsed?.Results is null
          ? (IReadOnlyList<SearXngImageResult>)[]
          : parsed.Results
              .Where(r => !string.IsNullOrEmpty(r.ImgSrc))
              .Take(maxResults)
              .Select(r => new SearXngImageResult
              {
                Title = r.Title ?? string.Empty,
                ImageUrl = r.ImgSrc!,
                SourceUrl = r.Url ?? string.Empty,
                ThumbnailUrl = r.ThumbnailSrc,
                Resolution = r.Resolution
              })
              .ToList();

        return new SearXngSearchOutcome<SearXngImageResult>
        {
          Results = results,
          HttpStatus = response.StatusCode,
          UnresponsiveEngines = engines
        };
      }
      catch(HttpRequestException ex)
      {
        _logger.LogError(ex, "SearXNG image search HTTP request failed for query: {Query}", query);
        return new SearXngSearchOutcome<SearXngImageResult> { TransportError = ex.Message };
      }
      catch(TaskCanceledException ex)
      {
        _logger.LogError(ex, "SearXNG image search timed out for query: {Query}", query);
        return new SearXngSearchOutcome<SearXngImageResult> { TransportError = ex.Message };
      }
      catch(JsonException ex)
      {
        _logger.LogError(ex, "SearXNG image search response parse failed for query: {Query}", query);
        return new SearXngSearchOutcome<SearXngImageResult> { TransportError = ex.Message };
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "SearXNG image search failed unexpectedly for query: {Query}", query);
        return new SearXngSearchOutcome<SearXngImageResult> { TransportError = ex.Message };
      }
    }

    private static bool ShouldFallback<T>(SearXngSearchOutcome<T> outcome)
      => outcome.Results.Count == 0
        && outcome.UnresponsiveEngines.Count > 0
        && outcome.HttpStatus is not null
        && (int)outcome.HttpStatus.Value >= 200
        && (int)outcome.HttpStatus.Value < 300;

    private static SearXngSearchOutcome<T> MergeOutcomes<T>(
      SearXngSearchOutcome<T> first,
      SearXngSearchOutcome<T> second
    )
    {
      var mergedEngines = first.UnresponsiveEngines
        .Concat(second.UnresponsiveEngines)
        .DistinctBy(e => e.Name)
        .ToList();

      return new SearXngSearchOutcome<T>
      {
        Results = second.Results,
        HttpStatus = second.HttpStatus,
        TransportError = second.TransportError,
        UnresponsiveEngines = mergedEngines
      };
    }

    private class SearXngResponse
    {
      public List<SearXngResult>? Results { get; set; }

      [JsonPropertyName("number_of_results")]
      public int? NumberOfResults { get; set; }

      [JsonPropertyName("unresponsive_engines")]
      [JsonConverter(typeof(UnresponsiveEngineListConverter))]
      public List<UnresponsiveEngine> UnresponsiveEngines { get; set; } = [];
    }

    private class SearXngResult
    {
      public string? Title { get; set; }
      public string? Url { get; set; }
      public string? Content { get; set; }
    }

    private class SearXngImageResponse
    {
      public List<SearXngImageItem>? Results { get; set; }

      [JsonPropertyName("number_of_results")]
      public int? NumberOfResults { get; set; }

      [JsonPropertyName("unresponsive_engines")]
      [JsonConverter(typeof(UnresponsiveEngineListConverter))]
      public List<UnresponsiveEngine> UnresponsiveEngines { get; set; } = [];
    }

    private class SearXngImageItem
    {
      public string? Title { get; set; }
      public string? Url { get; set; }

      [JsonPropertyName("img_src")]
      public string? ImgSrc { get; set; }

      [JsonPropertyName("thumbnail_src")]
      public string? ThumbnailSrc { get; set; }

      public string? Resolution { get; set; }
    }

    private class UnresponsiveEngineListConverter: JsonConverter<List<UnresponsiveEngine>>
    {
      public override List<UnresponsiveEngine> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
      )
      {
        var list = new List<UnresponsiveEngine>();

        if(reader.TokenType != JsonTokenType.StartArray)
        {
          return list;
        }

        while(reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
          if(reader.TokenType != JsonTokenType.StartArray)
          {
            reader.Skip();
            continue;
          }

          string? name = null;
          string? error = null;
          int index = 0;

          while(reader.Read() && reader.TokenType != JsonTokenType.EndArray)
          {
            if(reader.TokenType == JsonTokenType.String)
            {
              if(index == 0)
              {
                name = reader.GetString();
              }
              else if(index == 1)
              {
                error = reader.GetString();
              }
            }
            index++;
          }

          if(name is not null)
          {
            list.Add(new UnresponsiveEngine(name, error ?? string.Empty));
          }
        }

        return list;
      }

      public override void Write(
        Utf8JsonWriter writer,
        List<UnresponsiveEngine> value,
        JsonSerializerOptions options
      )
      {
        writer.WriteStartArray();
        foreach(var engine in value)
        {
          writer.WriteStartArray();
          writer.WriteStringValue(engine.Name);
          writer.WriteStringValue(engine.Error);
          writer.WriteEndArray();
        }
        writer.WriteEndArray();
      }
    }
  }

}
