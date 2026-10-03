namespace SlopChat.Models {

  public record CompletionResult(string Text, IReadOnlyList<byte[]> Images);

}
