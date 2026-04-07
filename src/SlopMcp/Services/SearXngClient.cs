using System.Text.Json;
using SlopMcp.Models;

namespace SlopMcp.Services {

  public class SearXngClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<SearXngClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNameCaseInsensitive = true
    };

    public SearXngClient(HttpClient httpClient, ILogger<SearXngClient> logger)
    {
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 5, CancellationToken ct = default)
    {
      try
      {
        string encodedQuery = Uri.EscapeDataString(query);
        string url = $"search?q={encodedQuery}&format=json";

        using HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        SearXngResponse? parsed = JsonSerializer.Deserialize<SearXngResponse>(json, JsonOptions);

        if(parsed?.Results is null)
        {
          return [];
        }

        return parsed.Results
          .Take(maxResults)
          .Select(r => new SearchResult
          {
            Title = r.Title ?? string.Empty,
            Url = r.Url ?? string.Empty,
            Content = r.Content ?? string.Empty
          })
          .ToList();
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "SearXNG search failed for query: {Query}", query);
        return [];
      }
    }

    private class SearXngResponse
    {
      public List<SearXngResult>? Results { get; set; }
    }

    private class SearXngResult
    {
      public string? Title { get; set; }
      public string? Url { get; set; }
      public string? Content { get; set; }
    }
  }

}
