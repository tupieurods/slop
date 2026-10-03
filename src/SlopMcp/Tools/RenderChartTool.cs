using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class RenderChartTool
  {
    private readonly ChartRenderer _renderer;
    private readonly ILogger<RenderChartTool> _logger;

    public RenderChartTool(ChartRenderer renderer, ILogger<RenderChartTool> logger)
    {
      _renderer = renderer;
      _logger = logger;
    }

    [McpServerTool(Name = "render_chart")]
    [Description("Render a chart (line, bar, scatter or pie) as a PNG image. The image is delivered to the user automatically as a photo; do not paste links or base64, just comment on the chart in your reply.")]
    public IEnumerable<ContentBlock> Render(
      [Description("Chart type: line, bar, scatter or pie.")] string type,
      [Description("Data series. Line/bar/scatter accept several series (shown in a legend); pie requires exactly one series.")] ChartSeries[] series,
      [Description("Optional chart title.")] string? title = null,
      [Description("Optional X axis label.")] string? xLabel = null,
      [Description("Optional Y axis label.")] string? yLabel = null,
      [Description("Optional category labels (one per value) used as X tick labels for line/bar charts and as slice names for pie charts.")] string[]? categories = null
    )
    {
      try
      {
        byte[] png = _renderer.Render(type, series, title, xLabel, yLabel, categories);
        return VisualizationToolResult.Image(png, "Chart rendered and sent to the user.");
      }
      catch(ArgumentException ex)
      {
        _logger.LogWarning("render_chart validation failed: {Message}", ex.Message);
        return VisualizationToolResult.Error($"Chart was not rendered: {ex.Message}");
      }
    }
  }

}
