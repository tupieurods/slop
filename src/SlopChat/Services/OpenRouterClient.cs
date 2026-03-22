using System.ClientModel;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;

namespace SlopChat.Services
{
  public class OpenRouterClient
  {
    private readonly ChatClient _chatClient;
    private readonly OpenAIClient _openAiClient;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(string apiKey, string defaultModel, ILogger<OpenRouterClient> logger)
    {
      _logger = logger;

      OpenAIClientOptions options = new()
      {
        Endpoint = new Uri("https://openrouter.ai/api/v1")
      };

      _openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
      _chatClient = _openAiClient.GetChatClient(defaultModel);
    }

    public async Task<string> GetCompletionAsync(List<ChatMessage> messages, CancellationToken ct)
    {
      try
      {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct);
        string content = completion.Content[0].Text;
        return content;
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
        OpenAIModelClient modelClient = _openAiClient.GetOpenAIModelClient();
        ClientResult<OpenAIModelCollection> response = await modelClient.GetModelsAsync(cancellationToken: ct);
        List<string> result = new();

        foreach(var model in response.Value)
        {
          result.Add(model.Id);
        }

        return result;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch models from OpenRouter");
        return new List<string>();
      }
    }
  }
}
