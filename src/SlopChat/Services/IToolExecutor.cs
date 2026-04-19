using SlopChat.Models;

namespace SlopChat.Services {

  public interface IToolExecutor
  {
    Task<IReadOnlyList<ToolDefinition>> GetToolDefinitionsAsync(CancellationToken ct);
    Task<string> ExecuteAsync(string toolName, string arguments, CancellationToken ct);
  }

}
