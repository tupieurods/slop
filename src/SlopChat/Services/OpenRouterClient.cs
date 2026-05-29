using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, ImageModelInfo> _imageModelCache = new();

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

    public virtual async Task<string> GetCompletionAsync(
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
        const int maxIterations = 8;
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

          bool isToolCallFinish = choice.FinishReason == "tool_calls";
          bool hasToolCallsInMessage = choice.Message?.ToolCalls is { Count: > 0 };

          if(isToolCallFinish && hasToolCallsInMessage && toolExecutor is not null)
          {
            workingMessages.Add(ChatMessage.Assistant(choice.Message!.ToolCalls!));
            foreach(Models.ToolCall toolCall in choice.Message!.ToolCalls!)
            {
              _logger.LogInformation("Executing tool {ToolName}", toolCall.Function.Name);
              string result = await toolExecutor.ExecuteAsync(toolCall.Function.Name, toolCall.Function.Arguments, ct);
              workingMessages.Add(ChatMessage.Tool(toolCall.Id, result));
              toolCallCount++;
            }
            continue;
          }

          if(isToolCallFinish && !hasToolCallsInMessage)
          {
            _logger.LogWarning(
              "Empty model response recovery: finish_reason=tool_calls but ToolCalls is null/empty, model={Model}, finishReason={FinishReason}, nativeFinishReason={NativeFinishReason}; routing to force-final",
              model, choice.FinishReason, choice.NativeFinishReason);
            break;
          }

          if(!string.IsNullOrEmpty(choice.Message?.Content))
          {
            return ResolveFinalText(choice.Message, toolCallCount);
          }

          if(!string.IsNullOrEmpty(choice.Message?.Reasoning))
          {
            string? summarized = await TrySummarizeReasoningAsync(workingMessages, choice.Message!.Reasoning!, model, toolCallCount, ct);
            if(summarized is not null)
            {
              return summarized;
            }
            _logger.LogWarning(
              "Empty model response recovery: in-loop content empty, model={Model}, finishReason={FinishReason}, nativeFinishReason={NativeFinishReason}; using level-{Level}",
              model, choice.FinishReason, choice.NativeFinishReason, "4 (reasoning)");
          }
          else
          {
            _logger.LogWarning(
              "Empty model response recovery: in-loop content empty, model={Model}, finishReason={FinishReason}, nativeFinishReason={NativeFinishReason}; using level-{Level}",
              model, choice.FinishReason, choice.NativeFinishReason, "5 (placeholder)");
          }

          return ResolveFinalText(choice.Message, toolCallCount);
        }

        _logger.LogWarning("Reached max tool call iterations ({Max}), forcing final response", maxIterations);

        const string nudge = "You have used the tool budget. Based ONLY on the tool results above, give the user a final plain-text answer now. Do not call any more tools.";

        var level2Messages = new List<ChatMessage>(workingMessages) { ChatMessage.User(nudge) };
        var level2Request = new ChatCompletionRequest
        {
          Model = model,
          Messages = level2Messages,
          Tools = tools,
          ToolChoice = "none"
        };
        ChatCompletionResponse level2Response = await SendCompletionRequestAsync(level2Request, ct);
        ChatChoice level2Choice = level2Response.Choices.FirstOrDefault()
                                  ?? throw new InvalidOperationException("OpenRouter returned no choices in level-2 retry");
        ChatChoiceMessage? level2Message = level2Choice.Message;

        if(!string.IsNullOrEmpty(level2Message?.Content))
        {
          _logger.LogWarning(
            "Empty model response recovery: level-2 (force-final, tool_choice=none) produced text, model={Model}, finishReason={FinishReason}, nativeFinishReason={NativeFinishReason}",
            model, level2Choice.FinishReason, level2Choice.NativeFinishReason);
          return ResolveFinalText(level2Message, toolCallCount);
        }

        string level45 = !string.IsNullOrEmpty(level2Message?.Reasoning) ? "4 (reasoning)" : "5 (placeholder)";
        _logger.LogWarning(
          "Empty model response recovery: level-2 returned empty content, model={Model}, finishReason={FinishReason}, nativeFinishReason={NativeFinishReason}; using level-{Level}",
          model, level2Choice.FinishReason, level2Choice.NativeFinishReason, level45);

        return ResolveFinalText(level2Message, toolCallCount);
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


    public async Task<List<ImageModelInfo>> GetImageModelsAsync(CancellationToken ct)
    {
      try
      {
        using HttpResponseMessage response = await _httpClient.GetAsync("models?output_modalities=image", ct);
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct);
        ModelListResponse? modelList = JsonSerializer.Deserialize<ModelListResponse>(json, JsonOptions);

        var models = modelList?.Data
          .Select(m => new ImageModelInfo
          {
            Id = m.Id,
            CanOutputText = m.Architecture?.OutputModalities?.Contains("text", StringComparer.OrdinalIgnoreCase) == true
          })
          .ToList() ?? [];

        _imageModelCache.Clear();
        foreach(var model in models)
        {
          _imageModelCache[model.Id] = model;
        }

        return models;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch image models from OpenRouter");
        return [];
      }
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(string prompt, string model, CancellationToken ct)
    {
      try
      {
        bool canOutputText = !_imageModelCache.TryGetValue(model, out var cached) || cached.CanOutputText;

        var messages = new List<ChatMessage>();
        if(canOutputText)
        {
          messages.Add(ChatMessage.System(
            "You are an image generation assistant. Generate an image based on the user's prompt. Do not ask clarifying questions — just create the image."));
        }
        messages.Add(ChatMessage.User(prompt));

        var request = new ChatCompletionRequest
        {
          Model = model,
          Messages = messages,
          Modalities = canOutputText ? ["image", "text"] : ["image"],
          Usage = new UsageOptions { Include = true }
        };

        ChatCompletionResponse response = await SendCompletionRequestAsync(request, ct);
        return ExtractImageFromResponse(response);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Image generation error");
        return ImageGenerationResult.Failure($"Image generation error: {ex.Message}");
      }
    }

    public async Task<ImageGenerationResult> GenerateImageFromImageAsync(
      string prompt,
      string model,
      string imageDataUrl,
      CancellationToken ct
    )
    {
      try
      {
        bool canOutputText = !_imageModelCache.TryGetValue(model, out var cached) || cached.CanOutputText;

        var userMessage = ChatMessage.UserMultimodal(
        [
          ContentPart.TextContent(prompt),
          ContentPart.Image(imageDataUrl)
        ]);

        var messages = new List<ChatMessage>();
        if(canOutputText)
        {
          messages.Add(ChatMessage.System(
            "You are an image generation assistant. Generate an image based on the user's prompt and the provided reference image. Do not ask clarifying questions — just create the image."));
        }
        messages.Add(userMessage);

        var request = new ChatCompletionRequest
        {
          Model = model,
          Messages = messages,
          Modalities = canOutputText ? ["image", "text"] : ["image"],
          Usage = new UsageOptions { Include = true }
        };

        ChatCompletionResponse response = await SendCompletionRequestAsync(request, ct);
        return ExtractImageFromResponse(response);
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Image-to-image generation error");
        return ImageGenerationResult.Failure($"Image-to-image generation error: {ex.Message}");
      }
    }

    private ImageGenerationResult ExtractImageFromResponse(ChatCompletionResponse response)
    {
      double? cost = response.Usage?.Cost;
      ChatChoiceMessage? message = response.Choices.FirstOrDefault()?.Message;
      if(message is null)
      {
        _logger.LogWarning("Image generation response contained no message");
        return ImageGenerationResult.Failure("No response from model.");
      }

      if(message.Images is { Count: > 0 })
      {
        string? dataUrl = message.Images[0].ImageUrl?.Url;
        if(dataUrl is not null)
        {
          int base64Start = dataUrl.IndexOf("base64,", StringComparison.Ordinal);
          if(base64Start >= 0)
          {
            string base64 = dataUrl[(base64Start + "base64,".Length)..];
            try
            {
              byte[] bytes = Convert.FromBase64String(base64);
              return ImageGenerationResult.Success(bytes, message.Content, cost);
            }
            catch(FormatException ex)
            {
              _logger.LogWarning(ex, "Failed to decode base64 image data");
            }
          }
          else
          {
            _logger.LogWarning("Image URL is not a base64 data URL");
          }
        }
      }

      if(!string.IsNullOrEmpty(message.Content))
      {
        _logger.LogWarning("Image generation returned text instead of image");
        return ImageGenerationResult.TextOnly(message.Content, cost);
      }

      _logger.LogWarning("Image generation response contained no images and no text");
      return ImageGenerationResult.Failure("Model returned empty response.");
    }

    private async Task<string?> TrySummarizeReasoningAsync(
      List<ChatMessage> history,
      string reasoningText,
      string model,
      int toolCallCount,
      CancellationToken ct)
    {
      try
      {
        var newMessages = new List<ChatMessage>(history)
        {
          ChatMessage.System(
            "The user asked a question and the assistant produced internal reasoning but failed to deliver a final answer. Below the user will paste that raw reasoning. Produce a clear, concise final answer for the user in plain text. Do not call any tools. Do not repeat or quote the reasoning itself."),
          ChatMessage.User(
            "Here is the raw reasoning from your previous attempt. Based on it, give the user a final answer now:\n\n" + reasoningText)
        };

        var request = new ChatCompletionRequest
        {
          Model = model,
          Messages = newMessages,
          Tools = null,
          ToolChoice = "none"
        };

        ChatCompletionResponse response = await SendCompletionRequestAsync(request, ct);
        ChatChoice? choice = response.Choices.FirstOrDefault();
        string? content = choice?.Message?.Content;

        if(!string.IsNullOrEmpty(content))
        {
          _logger.LogInformation(
            "Empty model response recovery: summarize-reasoning produced text, model={Model}, finishReason={FinishReason}",
            model, choice!.FinishReason);
          return toolCallCount > 0
            ? string.Concat(Enumerable.Repeat("🔧", toolCallCount)) + " " + content
            : content;
        }

        _logger.LogWarning(
          "Empty model response recovery: summarize-reasoning returned empty content, model={Model}, finishReason={FinishReason}",
          model, choice?.FinishReason);
        return null;
      }
      catch(Exception ex)
      {
        _logger.LogWarning(ex, "Empty model response recovery: summarize-reasoning threw an exception, model={Model}", model);
        return null;
      }
    }

    internal static string ResolveFinalText(ChatChoiceMessage? message, int toolCallCount)
    {
      string text;
      if(!string.IsNullOrEmpty(message?.Content))
      {
        text = message.Content;
      }
      else if(!string.IsNullOrEmpty(message?.Reasoning))
      {
        text = "💭 " + message.Reasoning;
      }
      else
      {
        text = "(no response)";
      }

      return toolCallCount > 0
        ? string.Concat(Enumerable.Repeat("🔧", toolCallCount)) + " " + text
        : text;
    }

    private async Task<ChatCompletionResponse> SendCompletionRequestAsync(ChatCompletionRequest request, CancellationToken ct)
    {
      string json = JsonSerializer.Serialize(request, JsonOptions);
      _logger.LogDebug("OpenRouter request: {Json}", json);

      using var content = new StringContent(json, Encoding.UTF8, "application/json");

      using HttpResponseMessage response = await _httpClient.PostAsync("chat/completions", content, ct);
      string responseJson = await response.Content.ReadAsStringAsync(ct);
      _logger.LogDebug("OpenRouter response: {Json}", responseJson.Trim());

      if(!response.IsSuccessStatusCode)
      {
        throw new HttpRequestException($"OpenRouter API returned {(int)response.StatusCode}: {responseJson}");
      }

      return JsonSerializer.Deserialize<ChatCompletionResponse>(responseJson, JsonOptions)
             ?? throw new InvalidOperationException("Failed to deserialize OpenRouter response");
    }
  }
}
