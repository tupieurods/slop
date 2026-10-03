using System.ComponentModel;

namespace SlopMcp.Models.Visualization {

  public sealed record DiagramNode
  {
    [Description("Unique node identifier referenced by edges.")]
    public required string Id { get; init; }

    [Description("Text displayed inside the node. Defaults to Id. Use \n for line breaks.")]
    public string? Label { get; init; }

    [Description("Node shape: box (default), rounded, ellipse, diamond.")]
    public string? Shape { get; init; }

    [Description("Optional fill color as hex, e.g. #FFCC00.")]
    public string? Color { get; init; }
  }

}
