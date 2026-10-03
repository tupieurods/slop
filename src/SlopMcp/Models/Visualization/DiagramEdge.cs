using System.ComponentModel;

namespace SlopMcp.Models.Visualization {

  public sealed record DiagramEdge
  {
    [Description("Id of the source node.")]
    public required string From { get; init; }

    [Description("Id of the target node.")]
    public required string To { get; init; }

    [Description("Optional text shown on the edge.")]
    public string? Label { get; init; }
  }

}
