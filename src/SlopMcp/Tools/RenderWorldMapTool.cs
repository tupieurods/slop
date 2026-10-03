using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class RenderWorldMapTool
  {
    private readonly WorldMapRenderer _renderer;
    private readonly ILogger<RenderWorldMapTool> _logger;

    public RenderWorldMapTool(WorldMapRenderer renderer, ILogger<RenderWorldMapTool> logger)
    {
      _renderer = renderer;
      _logger = logger;
    }

    [McpServerTool(Name = "render_world_map")]
    [Description("Render a world map (choropleth) with countries colored by a statistic or by explicit colors, as a PNG image. Countries without data are gray. The image is delivered to the user automatically as a photo; do not paste links or base64, just comment on it in your reply.")]
    public IEnumerable<ContentBlock> Render(
      [Description("Per-country data. Prefer ISO 3166-1 alpha-3 codes (USA, FRA, RUS).")] CountryValue[] values,
      [Description("Optional map title.")] string? title = null,
      [Description("Optional label for the color bar, e.g. 'Population, millions'.")] string? legendLabel = null,
      [Description("Colormap for numeric values: viridis (default), plasma, inferno, magma, turbo, blues, greens, reds, balance.")] string? colormap = null
    )
    {
      try
      {
        WorldMapResult result = _renderer.Render(values, title, legendLabel, colormap);
        string text = result.UnresolvedCountries.Count == 0
          ? "World map rendered and sent to the user."
          : $"World map rendered and sent to the user. Unresolved countries (not shown): {string.Join(", ", result.UnresolvedCountries)}.";
        return VisualizationToolResult.Image(result.Png, text);
      }
      catch(ArgumentException ex)
      {
        _logger.LogWarning("render_world_map validation failed: {Message}", ex.Message);
        return VisualizationToolResult.Error($"World map was not rendered: {ex.Message}");
      }
    }
  }

}
