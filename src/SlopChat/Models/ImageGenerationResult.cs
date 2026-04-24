namespace SlopChat.Models
{
  public class ImageGenerationResult
  {
    public byte[]? ImageBytes { get; private init; }
    public string? TextResponse { get; private init; }
    public string? ErrorMessage { get; private init; }
    public double? Cost { get; private init; }

    public bool HasImage => ImageBytes is { Length: > 0 };
    public bool HasText => !string.IsNullOrEmpty(TextResponse);

    public static ImageGenerationResult Success(byte[] imageBytes, string? textResponse = null, double? cost = null) => new()
    {
      ImageBytes = imageBytes,
      TextResponse = textResponse,
      Cost = cost
    };

    public static ImageGenerationResult TextOnly(string textResponse, double? cost = null) => new()
    {
      TextResponse = textResponse,
      Cost = cost
    };

    public static ImageGenerationResult Failure(string errorMessage) => new()
    {
      ErrorMessage = errorMessage
    };
  }
}
