namespace SlopChat.Models {

  public record ToolExecutionResult(string Text, IReadOnlyList<byte[]> Images)
  {
    public static ToolExecutionResult TextOnly(string text) => new(text, []);
  }

}
