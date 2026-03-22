using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SlopChat.Models;

namespace SlopChat.Services
{
    public class OpenRouterClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenRouterClient> _logger;

        public OpenRouterClient(HttpClient httpClient, ILogger<OpenRouterClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GetCompletionAsync(List<ChatMessage> messages, string model, CancellationToken ct)
        {
            OpenRouterRequest request = new()
            {
                Model = model,
                Messages = messages
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("chat/completions", request, ct);

            if(!response.IsSuccessStatusCode)
            {
                string errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("OpenRouter API error {StatusCode}: {Body}", response.StatusCode, errorBody);
                return $"OpenRouter API error: {response.StatusCode}";
            }

            OpenRouterResponse? result = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(ct);

            if(result?.Choices is { Count: > 0 })
            {
                return result.Choices[0].Message.Content;
            }

            _logger.LogWarning("OpenRouter returned empty choices");
            return "No response from the model.";
        }

        public async Task<List<OpenRouterModel>> GetModelsAsync(CancellationToken ct)
        {
            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync("models", ct);

                if(!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("OpenRouter models API error {StatusCode}: {Body}", response.StatusCode, errorBody);
                    return new List<OpenRouterModel>();
                }

                OpenRouterModelsResponse? result = await response.Content.ReadFromJsonAsync<OpenRouterModelsResponse>(ct);
                return result?.Data ?? new List<OpenRouterModel>();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch models from OpenRouter");
                return new List<OpenRouterModel>();
            }
        }
    }
}
