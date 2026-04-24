using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SlopChat.Models;

namespace SlopChat.Services
{
  public class OpenRouterVideoClient
  {
    private readonly HttpClient _httpClient;
    private readonly HttpClient _downloadClient;
    private readonly ILogger<OpenRouterVideoClient> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
      DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenRouterVideoClient(
      HttpClient httpClient,
      HttpClient downloadClient,
      string apiKey,
      TimeProvider timeProvider,
      ILogger<OpenRouterVideoClient> logger
    )
    {
      _httpClient = httpClient;
      _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
      _downloadClient = downloadClient;
      _timeProvider = timeProvider;
      _logger = logger;
    }

    public async Task<List<VideoModelInfo>> GetVideoModelsAsync(CancellationToken ct)
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync("videos/models", ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        var listResponse = JsonSerializer.Deserialize<VideoModelListResponse>(json, JsonOptions);
        return listResponse?.Data ?? [];
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch video models from OpenRouter");
        return [];
      }
    }

    public async Task<VideoGenerationResult> GenerateVideoAsync(
      string prompt,
      string model,
      string? firstFrameDataUrl,
      CancellationToken ct
    )
    {
      try
      {
        var request = new VideoGenerationRequest
        {
          Model = model,
          Prompt = prompt
        };

        if(!string.IsNullOrEmpty(firstFrameDataUrl))
        {
          request.FrameImages =
          [
            new FrameImage
            {
              Type = "image_url",
              ImageUrl = new FrameImageUrl { Url = firstFrameDataUrl },
              FrameType = "first_frame"
            }
          ];
        }

        string requestJson = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("Video generation request: {Json}", requestJson);

        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using HttpResponseMessage submitResponse = await _httpClient.PostAsync("videos", content, ct);
        string submitJson = await submitResponse.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Video generation submit response: {Json}", submitJson);

        if(!submitResponse.IsSuccessStatusCode)
        {
          return VideoGenerationResult.Failure($"Video submission failed ({(int)submitResponse.StatusCode}): {submitJson}");
        }

        var submitResult = JsonSerializer.Deserialize<VideoGenerationSubmitResponse>(submitJson, JsonOptions)
          ?? throw new InvalidOperationException("Failed to deserialize video submit response");

        if(string.Equals(submitResult.Status, "failed", StringComparison.OrdinalIgnoreCase))
        {
          return VideoGenerationResult.Failure("Video generation failed immediately after submission.");
        }

        string videoId = submitResult.Id;

        if(string.IsNullOrEmpty(videoId))
        {
          _logger.LogWarning("Video submission returned no job ID");
          return VideoGenerationResult.Failure("Video submission returned no job ID.");
        }

        _logger.LogInformation("Video generation submitted, id={Id}, polling...", videoId);

        return await PollForVideoAsync(videoId, submitResult.PollingUrl, ct);
      }
      catch(OperationCanceledException)
      {
        return VideoGenerationResult.Failure("Video generation timed out or was cancelled.");
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Video generation error");
        return VideoGenerationResult.Failure($"Video generation error: {ex.Message}");
      }
    }

    private async Task<VideoGenerationResult> PollForVideoAsync(string videoId, string? pollingUrl, CancellationToken ct)
    {
      using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(20), _timeProvider);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
      var linkedCt = linkedCts.Token;

      const int MaxConsecutivePollFailures = 10;
      int consecutiveFailures = 0;

      while(true)
      {
        await Task.Delay(TimeSpan.FromSeconds(15), _timeProvider, linkedCt);

        try
        {
          using HttpResponseMessage pollResponse = !string.IsNullOrEmpty(pollingUrl)
            ? await _httpClient.GetAsync(new Uri(pollingUrl), linkedCt)
            : await _httpClient.GetAsync($"videos/{videoId}", linkedCt);
          string pollJson = await pollResponse.Content.ReadAsStringAsync(linkedCt);
          _logger.LogDebug("Video poll response for {Id}: {Json}", videoId, pollJson);

          if(!pollResponse.IsSuccessStatusCode)
          {
            _logger.LogWarning("Poll request failed for video {Id}: {Status}", videoId, (int)pollResponse.StatusCode);
            continue;
          }

          var pollResult = JsonSerializer.Deserialize<VideoGenerationPollResponse>(pollJson, JsonOptions);
          if(pollResult is null)
          {
            continue;
          }

          consecutiveFailures = 0;

          string status = pollResult.Status;
          _logger.LogDebug("Video {Id} status: {Status}", videoId, status);

          if(string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
          {
            string errorMsg = pollResult.Error ?? "Video generation failed.";
            return VideoGenerationResult.Failure(errorMsg);
          }

          if(string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
          {
            if(pollResult.UnsignedUrls is not { Count: > 0 })
            {
              return VideoGenerationResult.Failure("Video completed but no download URL was provided.");
            }

            string videoUrl = pollResult.UnsignedUrls[0];
            byte[] videoBytes = await DownloadVideoBytesAsync(videoUrl, linkedCt);
            _logger.LogInformation("Video {Id} downloaded, {Bytes} bytes", videoId, videoBytes.Length);
            return VideoGenerationResult.Success(videoBytes, pollResult.Usage?.Cost);
          }
        }
        catch(Exception ex) when((ex is HttpRequestException or IOException or JsonException or TaskCanceledException) && !linkedCt.IsCancellationRequested)
        {
          _logger.LogWarning(ex, "Transient error while polling for video {Id}", videoId);
          consecutiveFailures++;
          if(consecutiveFailures >= MaxConsecutivePollFailures)
          {
            return VideoGenerationResult.Failure($"Polling failed repeatedly: {ex.Message}");
          }
        }
      }
    }

    private async Task<byte[]> DownloadVideoBytesAsync(string url, CancellationToken ct)
    {
      using HttpResponseMessage response = await _downloadClient.GetAsync(url, ct);
      response.EnsureSuccessStatusCode();
      return await response.Content.ReadAsByteArrayAsync(ct);
    }

    private class VideoModelListResponse
    {
      [JsonPropertyName("data")]
      public List<VideoModelInfo> Data { get; set; } = [];
    }
  }
}
