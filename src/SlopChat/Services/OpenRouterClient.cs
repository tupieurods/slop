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
            return choice.Message?.Content ?? string.Empty;
          }

          workingMessages.Add(ChatMessage.Assistant(choice.Message.ToolCalls));

          foreach(Models.ToolCall toolCall in choice.Message.ToolCalls)
          {
            _logger.LogInformation("Executing tool {ToolName}", toolCall.Function.Name);
            string result = await toolExecutor.ExecuteAsync(toolCall.Function.Name, toolCall.Function.Arguments, ct);
            workingMessages.Add(ChatMessage.Tool(toolCall.Id, result));
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
        return finalChoice.Message?.Content ?? string.Empty;
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
