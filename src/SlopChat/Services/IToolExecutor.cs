using SlopChat.Models;

namespace SlopChat.Services {

  public interface IToolExecutor
  {
    Task<IReadOnlyList<ToolDefinition>> GetToolDefinitionsAsync(CancellationToken ct);
    Task<ToolExecutionResult> ExecuteAsync(string toolName, string arguments, CancellationToken ct);
  }

}
