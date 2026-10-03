using ScottPlot;

namespace SlopMcp.Services.Visualization {

  internal sealed class ColorAxisSource: IHasColorAxis
  {
    private readonly ScottPlot.Range _range;

    public IColormap Colormap { get; set; }

    public ColorAxisSource(IColormap colormap, double min, double max)
    {
      Colormap = colormap;
      _range = new ScottPlot.Range(min, max);
    }

    public ScottPlot.Range GetRange() => _range;
  }

}
