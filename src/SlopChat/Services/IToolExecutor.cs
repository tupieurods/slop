using OpenAI.Chat;

namespace SlopChat.Services {

  public interface IToolExecutor
  {
    Task<IReadOnlyList<ChatTool>> GetChatToolsAsync(CancellationToken ct);
    Task<string> ExecuteAsync(string toolName, BinaryData arguments, CancellationToken ct);
  }

}
