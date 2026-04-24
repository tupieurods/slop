namespace SlopChat.Models
{
  public class VideoGenerationResult
  {
    public byte[]? VideoBytes { get; private init; }
    public string? ErrorMessage { get; private init; }
    public double? Cost { get; private init; }

    public bool HasVideo => VideoBytes is { Length: > 0 };

    public static VideoGenerationResult Success(byte[] videoBytes, double? cost = null) => new()
    {
      VideoBytes = videoBytes,
      Cost = cost
    };

    public static VideoGenerationResult Failure(string errorMessage) => new()
    {
      ErrorMessage = errorMessage
    };
  }
}
