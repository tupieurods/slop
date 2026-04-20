using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SlopChat.Models;

namespace SlopChat.Services
{
  public class OpenRouterClient
  {
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public OpenRouterClient(HttpClient httpClient, string apiKey, ILogger<OpenRouterClient> logger)
    {
      _httpClient = httpClient;
      _httpClient.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
      _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
      _logger = logger;
    }

    public async Task<string> GetCompletionAsync(
      List<ChatMessage> messages,
      string model,
      CancellationToken ct,
      IToolExecutor? toolExecutor = null
    )
    {
      try
      {
        List<ToolDefinition>? tools = null;
        if(toolExecutor is not null)
        {
          var defs = await toolExecutor.GetToolDefinitionsAsync(ct);
          if(defs.Count > 0)
          {
            tools = [..defs];
            _logger.LogInformation("Including {Count} tools in request", tools.Count);
          }
          else
          {
            _logger.LogWarning("Tool executor returned 0 tool definitions");
          }
        }

        var workingMessages = new List<ChatMessage>(messages);
        const int maxIterations = 5;
        int toolCallCount = 0;

        for(int i = 0; i < maxIterations; i++)
        {
          var request = new ChatCompletionRequest
          {
            Model = model,
            Messages = workingMessages,
            Tools = tools
          };

          ChatCompletionResponse response = await SendCompletionRequestAsync(request, ct);
          ChatChoice choice = response.Choices.FirstOrDefault()
                              ?? throw new InvalidOperationException("OpenRouter returned no choices");

          _logger.LogDebug("Finish reason: {FinishReason}, has tool calls: {HasToolCalls}",
            choice.FinishReason, choice.Message?.ToolCalls is not null);

          if(choice.FinishReason != "tool_calls" || toolExecutor is null || choice.Message?.ToolCalls is null)
          {
            string content = choice.Message?.Content ?? string.Empty;
            return toolCallCount > 0
              ? string.Concat(Enumerable.Repeat("🔧", toolCallCount)) + " " + content
              : content;
          }

          workingMessages.Add(ChatMessage.Assistant(choice.Message.ToolCalls));

          foreach(Models.ToolCall toolCall in choice.Message.ToolCalls)
          {
            _logger.LogInformation("Executing tool {ToolName}", toolCall.Function.Name);
            string result = await toolExecutor.ExecuteAsync(toolCall.Function.Name, toolCall.Function.Arguments, ct);
            workingMessages.Add(ChatMessage.Tool(toolCall.Id, result));
            toolCallCount++;
          }
        }

        _logger.LogWarning("Reached max tool call iterations ({Max}), forcing final response", maxIterations);
        var finalRequest = new ChatCompletionRequest
        {
          Model = model,
          Messages = workingMessages
        };

        ChatCompletionResponse finalResponse = await SendCompletionRequestAsync(finalRequest, ct);
        ChatChoice finalChoice = finalResponse.Choices.FirstOrDefault()
                                 ?? throw new InvalidOperationException("OpenRouter returned no choices");
        string finalContent = finalChoice.Message?.Content ?? string.Empty;
        return toolCallCount > 0
          ? string.Concat(Enumerable.Repeat("🔧", toolCallCount)) + " " + finalContent
          : finalContent;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "OpenRouter API error");
        return $"OpenRouter API error: {ex.Message}";
      }
    }

    public async Task<List<string>> GetModelsAsync(CancellationToken ct)
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync("models", ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        ModelListResponse? modelList = JsonSerializer.Deserialize<ModelListResponse>(json, JsonOptions);

        return modelList?.Data.Select(m => m.Id).ToList() ?? [];
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch models from OpenRouter");
        return [];
      }
    }


    public async Task<List<string>> GetImageModelsAsync(CancellationToken ct)
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync("models", ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        ModelListResponse? modelList = JsonSerializer.Deserialize<ModelListResponse>(json, JsonOptions);

        return modelList?.Data
          .Where(m => m.IsImageGeneration)
          .Select(m => m.Id)
          .ToList() ?? [];
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch image models from OpenRouter");
        return [];
      }
    }

    public async Task<byte[]?> GenerateImageAsync(string prompt, string model, CancellationToken ct)
    {
      try
      {
        var request = new ImageGenerationRequest
        {
          Model = model,
          Prompt = prompt
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("Image generation request: {Json}", json);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync("images/generations", content, ct);
        string responseJson = await response.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Image generation response length: {Length}", responseJson.Length);

        if(!response.IsSuccessStatusCode)
        {
          throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {responseJson}");
        }

        ImageGenerationResponse? result = JsonSerializer.Deserialize<ImageGenerationResponse>(responseJson, JsonOptions);
        ImageData? imageData = result?.Data.FirstOrDefault();

        if(imageData?.B64Json is not null)
        {
          return Convert.FromBase64String(imageData.B64Json);
        }

        if(imageData?.Url is not null)
        {
          return await _httpClient.GetByteArrayAsync(imageData.Url, ct);
        }

        _logger.LogWarning("Image generation returned no image data");
        return null;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Image generation error");
        return null;
      }
    }

    public async Task<byte[]?> GenerateImageFromImageAsync(
      string prompt,
      string model,
      string imageDataUrl,
      CancellationToken ct
    )
    {
      try
      {
        var messages = new List<ChatMessage>
        {
          ChatMessage.UserMultimodal(
          [
            ContentPart.TextContent(prompt),
            ContentPart.Image(imageDataUrl)
          ])
        };

        var request = new ChatCompletionRequest
        {
          Model = model,
          Messages = messages
        };

        string json = JsonSerializer.Serialize(request, JsonOptions);
        _logger.LogDebug("Image-to-image request: {Json}", json.Length > 500 ? json[..500] + "..." : json);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content, ct);
        string responseJson = await response.Content.ReadAsStringAsync(ct);

        if(!response.IsSuccessStatusCode)
        {
          throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {responseJson}");
        }

        ChatCompletionResponse? completionResponse =
          JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions);
        string? responseContent = completionResponse?.Choices.FirstOrDefault()?.Message?.Content;

        if(responseContent is null)
        {
          _logger.LogWarning("Image-to-image returned no content");
          return null;
        }

        // Response may contain base64 image data in markdown format: ![...](data:image/png;base64,...)
        int dataUrlStart = responseContent.IndexOf("data:image/", StringComparison.Ordinal);
        if(dataUrlStart >= 0)
        {
          int base64Start = responseContent.IndexOf("base64,", dataUrlStart, StringComparison.Ordinal);
          if(base64Start >= 0)
          {
            base64Start += "base64,".Length;
            int base64End = responseContent.IndexOfAny([')', '"', ' ', '\n'], base64Start);
            if(base64End < 0)
            {
              base64End = responseContent.Length;
            }

            string base64 = responseContent[base64Start..base64End];
            return Convert.FromBase64String(base64);
          }
        }

        _logger.LogWarning("Image-to-image response did not contain image data, returning text response");
        return null;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Image-to-image generation error");
        return null;
      }
    }

    private async Task<ChatCompletionResponse> SendCompletionRequestAsync(ChatCompletionRequest request, CancellationToken ct)
    {
      string json = JsonSerializer.Serialize(request, JsonOptions);
      _logger.LogDebug("OpenRouter request: {Json}", json);

      using var content = new StringContent(json, Encoding.UTF8, "application/json");

      using HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content, ct);
      string responseJson = await response.Content.ReadAsStringAsync(ct);
      _logger.LogDebug("OpenRouter response: {Json}", responseJson);

      if(!response.IsSuccessStatusCode)
      {
        throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {responseJson}");
      }

      return JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions)
             ?? throw new InvalidOperationException("Failed to deserialize OpenRouter response");
    }
  }
}
