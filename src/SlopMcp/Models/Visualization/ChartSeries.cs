using System.ComponentModel;

namespace SlopMcp.Models.Visualization {

  public sealed record ChartSeries
  {
    [Description("Series name shown in the legend.")]
    public string? Name { get; init; }

    [Description("Y values of the series. For pie charts: slice values.")]
    public required double[] Values { get; init; }

    [Description("Optional numeric X values (same length as Values). Used by line and scatter charts; omit to use categories or indexes 0..N-1.")]
    public double[]? X { get; init; }
  }

}
