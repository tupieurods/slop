using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class GetCurrentDateTool
  {
    [McpServerTool(Name = "get_current_date"), Description("Returns the current date and time in UTC (ISO 8601 format).")]
    public string GetCurrentDate() => TimeProvider.System.GetUtcNow().ToString("o");
  }

}
