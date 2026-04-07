using System.ClientModel;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;

namespace SlopChat.Services;

public class OpenRouterClient
{
    private readonly OpenAIClient _openAiClient;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(string apiKey, ILogger<OpenRouterClient> logger)
    {
      _logger = logger;

      OpenAIClientOptions options = new()
      {
        Endpoint = new Uri("https://openrouter.ai/api/v1")
      };

      _openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);
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
        ChatClient chatClient = _openAiClient.GetChatClient(model);

        ChatCompletionOptions? completionOptions = null;
        if(toolExecutor is not null)
        {
          var tools = await toolExecutor.GetChatToolsAsync(ct);
          if(tools.Count > 0)
          {
            completionOptions = new ChatCompletionOptions();
            foreach(ChatTool tool in tools)
            {
              completionOptions.Tools.Add(tool);
            }
          }
        }

        var workingMessages = new List<ChatMessage>(messages);
        const int maxIterations = 5;

        for(int i = 0; i < maxIterations; i++)
        {
          ChatCompletion completion = completionOptions is not null
            ? await chatClient.CompleteChatAsync(workingMessages, completionOptions, ct)
            : await chatClient.CompleteChatAsync(workingMessages, cancellationToken: ct);

          if(completion.FinishReason != ChatFinishReason.ToolCalls || toolExecutor is null)
          {
            return completion.Content[0].Text;
          }

          workingMessages.Add(ChatMessage.CreateAssistantMessage(completion));

          foreach(ChatToolCall toolCall in completion.ToolCalls)
          {
            _logger.LogInformation("Executing tool {ToolName}", toolCall.FunctionName);
            string result = await toolExecutor.ExecuteAsync(toolCall.FunctionName, toolCall.FunctionArguments, ct);
            workingMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
          }
        }

        _logger.LogWarning("Reached max tool call iterations ({Max}), forcing final response", maxIterations);
        ChatCompletion finalCompletion = await chatClient.CompleteChatAsync(workingMessages, cancellationToken: ct);
        return finalCompletion.Content[0].Text;
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
