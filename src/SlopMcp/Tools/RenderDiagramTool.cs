using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class RenderDiagramTool
  {
    private readonly DiagramRenderer _renderer;
    private readonly ILogger<RenderDiagramTool> _logger;

    public RenderDiagramTool(DiagramRenderer renderer, ILogger<RenderDiagramTool> logger)
    {
      _renderer = renderer;
      _logger = logger;
    }

    [McpServerTool(Name = "render_diagram")]
    [Description("Render a diagram (flowchart, dependency graph, org chart, state machine, etc.) from nodes and directed edges as a PNG image with automatic layout. The image is delivered to the user automatically as a photo; do not paste links or base64, just comment on it in your reply.")]
    public IEnumerable<ContentBlock> Render(
      [Description("Diagram nodes.")] DiagramNode[] nodes,
      [Description("Directed edges between node ids.")] DiagramEdge[]? edges = null,
      [Description("Layout direction: TB (top to bottom, default) or LR (left to right).")] string? direction = null,
      [Description("Optional diagram title.")] string? title = null
    )
    {
      try
      {
        byte[] png = _renderer.Render(nodes, edges, direction, title);
        return VisualizationToolResult.Image(png, "Diagram rendered and sent to the user.");
      }
      catch(ArgumentException ex)
      {
        _logger.LogWarning("render_diagram validation failed: {Message}", ex.Message);
        return VisualizationToolResult.Error($"Diagram was not rendered: {ex.Message}");
      }
    }
  }

}
