using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SlopChat.Models;
using SlopChat.Services;

namespace SlopChat.Tests
{
  public class OpenRouterClientImagesApiTests
  {
    private const string ImageOnlyModel = "openai/gpt-image-2.5-flare";
    private const string TextImageModel = "google/gemini-image";
    private const string ModelsJson = $$"""
      {
        "data": [
          { "id": "{{ImageOnlyModel}}", "architecture": { "output_modalities": ["image"] } },
          { "id": "{{TextImageModel}}", "architecture": { "output_modalities": ["image", "text"] } }
        ]
      }
      """;

    private static readonly byte[] ImageBytes = [1, 2, 3, 4, 5];

    [Fact]
    public async Task GenerateImageAsync_ImageOnlyModel_UsesImagesApiWithoutReferences()
    {
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, ImagesSuccessResponse);

      ImageGenerationResult result = await client.GenerateImageAsync("a cat", ImageOnlyModel, CancellationToken.None);

      CapturedRequest imagesRequest = Assert.Single(requests, r => r.Path == "/api/v1/images");
      Assert.DoesNotContain(requests, r => r.Path == "/api/v1/chat/completions");
      using JsonDocument body = JsonDocument.Parse(imagesRequest.Body!);
      Assert.Equal(ImageOnlyModel, body.RootElement.GetProperty("model").GetString());
      Assert.Equal("a cat", body.RootElement.GetProperty("prompt").GetString());
      Assert.False(body.RootElement.TryGetProperty("input_references", out _));
      Assert.True(result.HasImage);
      Assert.Equal(ImageBytes, result.ImageBytes);
      Assert.Equal(0.04, result.Cost);
    }

    [Fact]
    public async Task GenerateImageFromImageAsync_ImageOnlyModel_SendsInputReference()
    {
      const string dataUrl = "data:image/png;base64,AAAA";
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, ImagesSuccessResponse);

      ImageGenerationResult result = await client.GenerateImageFromImageAsync("make it watercolor", ImageOnlyModel, dataUrl, CancellationToken.None);

      CapturedRequest imagesRequest = Assert.Single(requests, r => r.Path == "/api/v1/images");
      using JsonDocument body = JsonDocument.Parse(imagesRequest.Body!);
      JsonElement reference = body.RootElement.GetProperty("input_references")[0];
      Assert.Equal("image_url", reference.GetProperty("type").GetString());
      Assert.Equal(dataUrl, reference.GetProperty("image_url").GetProperty("url").GetString());
      Assert.Equal("make it watercolor", body.RootElement.GetProperty("prompt").GetString());
      Assert.True(result.HasImage);
    }

    [Fact]
    public async Task GenerateImageAsync_TextCapableModel_UsesChatCompletions()
    {
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, ChatSuccessResponse);

      ImageGenerationResult result = await client.GenerateImageAsync("a cat", TextImageModel, CancellationToken.None);

      Assert.DoesNotContain(requests, r => r.Path == "/api/v1/images");
      CapturedRequest chatRequest = Assert.Single(requests, r => r.Path == "/api/v1/chat/completions");
      using JsonDocument body = JsonDocument.Parse(chatRequest.Body!);
      string[] modalities = body.RootElement.GetProperty("modalities").EnumerateArray().Select(m => m.GetString()!).ToArray();
      Assert.Equal(["image", "text"], modalities);
      Assert.True(result.HasImage);
      Assert.Equal(ImageBytes, result.ImageBytes);
    }

    [Fact]
    public async Task GenerateImageAsync_ImageOnlyModelCalledTwice_FetchesModelsOnce()
    {
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, ImagesSuccessResponse);

      await client.GenerateImageAsync("a cat", ImageOnlyModel, CancellationToken.None);
      await client.GenerateImageAsync("a dog", ImageOnlyModel, CancellationToken.None);

      Assert.Single(requests, r => r.Path == "/api/v1/models");
      Assert.Equal(2, requests.Count(r => r.Path == "/api/v1/images"));
    }

    [Fact]
    public async Task GenerateImageAsync_UnknownModel_FallsBackToChatCompletions()
    {
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, ChatSuccessResponse);

      ImageGenerationResult result = await client.GenerateImageAsync("a cat", "unknown/model", CancellationToken.None);

      Assert.DoesNotContain(requests, r => r.Path == "/api/v1/images");
      Assert.Single(requests, r => r.Path == "/api/v1/chat/completions");
      Assert.True(result.HasImage);
    }

    [Fact]
    public async Task GenerateImageAsync_ImagesApiError_ReturnsFailureWithStatusCode()
    {
      var requests = new List<CapturedRequest>();
      var client = CreateClient(requests, () => JsonResponse(HttpStatusCode.BadRequest, """{"error":{"message":"bad prompt"}}"""));

      ImageGenerationResult result = await client.GenerateImageAsync("a cat", ImageOnlyModel, CancellationToken.None);

      Assert.False(result.HasImage);
      Assert.NotNull(result.ErrorMessage);
      Assert.Contains("400", result.ErrorMessage);
    }

    private static OpenRouterClient CreateClient(List<CapturedRequest> requests, Func<HttpResponseMessage> generationResponse)
    {
      var handler = new FuncHttpMessageHandler(request =>
      {
        string? body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
        requests.Add(new CapturedRequest(request.RequestUri!.AbsolutePath, body));

        if(request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "/api/v1/models")
        {
          return JsonResponse(HttpStatusCode.OK, ModelsJson);
        }

        return generationResponse();
      });

      return new OpenRouterClient(new HttpClient(handler), "key", NullLogger<OpenRouterClient>.Instance);
    }

    private static HttpResponseMessage ImagesSuccessResponse()
    {
      string json = $$"""
        {
          "data": [ { "b64_json": "{{Convert.ToBase64String(ImageBytes)}}", "media_type": "image/png" } ],
          "usage": { "cost": 0.04 }
        }
        """;
      return JsonResponse(HttpStatusCode.OK, json);
    }

    private static HttpResponseMessage ChatSuccessResponse()
    {
      string dataUrl = "data:image/png;base64," + Convert.ToBase64String(ImageBytes);
      string json = $$"""
        {
          "id": "gen-1",
          "model": "{{TextImageModel}}",
          "choices": [
            {
              "finish_reason": "stop",
              "message": {
                "role": "assistant",
                "content": "here you go",
                "images": [ { "type": "image_url", "image_url": { "url": "{{dataUrl}}" } } ]
              }
            }
          ],
          "usage": { "cost": 0.01 }
        }
        """;
      return JsonResponse(HttpStatusCode.OK, json);
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
      Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(string Path, string? Body);
  }
}
