using ScottPlot;
using SlopMcp.Models.Visualization;

namespace SlopMcp.Services.Visualization {

  public class ChartRenderer
  {
    public const int DefaultWidth = 1200;
    public const int DefaultHeight = 800;
    private const double GroupWidth = 0.8;

    private static readonly IPalette Palette = new ScottPlot.Palettes.Category10();

    public static IReadOnlyList<string> SupportedTypes { get; } = ["line", "bar", "scatter", "pie"];

    public byte[] Render(
      string type,
      IReadOnlyList<ChartSeries> series,
      string? title = null,
      string? xLabel = null,
      string? yLabel = null,
      IReadOnlyList<string>? categories = null,
      int width = DefaultWidth,
      int height = DefaultHeight
    )
    {
      string normalizedType = (type ?? string.Empty).Trim().ToLowerInvariant();
      if(!SupportedTypes.Contains(normalizedType))
      {
        throw new ArgumentException($"Unknown chart type '{type}'. Supported types: {string.Join(", ", SupportedTypes)}.", nameof(type));
      }

      Validate(normalizedType, series, categories);

      var plot = new Plot();
      switch(normalizedType)
      {
        case "line":
          AddLines(plot, series, categories, false);
          break;
        case "scatter":
          AddLines(plot, series, categories, true);
          break;
        case "bar":
          AddBars(plot, series, categories);
          break;
        case "pie":
          AddPie(plot, series[0], categories);
          break;
      }

      if(!string.IsNullOrWhiteSpace(title))
      {
        plot.Title(title);
      }

      if(normalizedType != "pie")
      {
        if(!string.IsNullOrWhiteSpace(xLabel))
        {
          plot.XLabel(xLabel);
        }

        if(!string.IsNullOrWhiteSpace(yLabel))
        {
          plot.YLabel(yLabel);
        }

        if(series.Count > 1)
        {
          plot.ShowLegend(Alignment.UpperRight);
        }
      }

      plot.Font.Automatic();
      return plot.GetImageBytes(width, height, ImageFormat.Png);
    }

    private static void Validate(string type, IReadOnlyList<ChartSeries>? series, IReadOnlyList<string>? categories)
    {
      if(series is null || series.Count == 0)
      {
        throw new ArgumentException("At least one series is required.", nameof(series));
      }

      for(int i = 0; i < series.Count; i++)
      {
        ChartSeries s = series[i];
        string name = s.Name ?? $"#{i + 1}";
        if(s.Values is null || s.Values.Length == 0)
        {
          throw new ArgumentException($"Series '{name}' has no values.", nameof(series));
        }

        if(s.Values.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
        {
          throw new ArgumentException($"Series '{name}' contains NaN or infinite values.", nameof(series));
        }

        if(s.X is not null && s.X.Length != s.Values.Length)
        {
          throw new ArgumentException($"Series '{name}': X has {s.X.Length} values but Values has {s.Values.Length}.", nameof(series));
        }

        if(categories is { Count: > 0 } && categories.Count != s.Values.Length)
        {
          throw new ArgumentException($"Series '{name}' has {s.Values.Length} values but {categories.Count} categories were given.", nameof(series));
        }
      }

      if(type == "pie")
      {
        if(series.Count != 1)
        {
          throw new ArgumentException("Pie chart requires exactly one series.", nameof(series));
        }

        if(series[0].Values.Any(v => v < 0))
        {
          throw new ArgumentException("Pie chart values must be non-negative.", nameof(series));
        }

        if(series[0].Values.Sum() <= 0)
        {
          throw new ArgumentException("Pie chart values must sum to a positive number.", nameof(series));
        }
      }
    }

    private static void AddLines(Plot plot, IReadOnlyList<ChartSeries> series, IReadOnlyList<string>? categories, bool markersOnly)
    {
      for(int i = 0; i < series.Count; i++)
      {
        ChartSeries s = series[i];
        double[] xs = s.X ?? Enumerable.Range(0, s.Values.Length).Select(x => (double)x).ToArray();
        ScottPlot.Plottables.Scatter scatter = plot.Add.Scatter(xs, s.Values, Palette.GetColor(i));
        scatter.LegendText = s.Name ?? string.Empty;
        scatter.LineWidth = markersOnly ? 0 : 2;
        scatter.MarkerSize = markersOnly ? 8 : 5;
      }

      bool usesCategoryTicks = categories is { Count: > 0 } && series.All(s => s.X is null);
      if(usesCategoryTicks)
      {
        SetCategoryTicks(plot, categories!);
      }
    }

    private static void AddBars(Plot plot, IReadOnlyList<ChartSeries> series, IReadOnlyList<string>? categories)
    {
      int seriesCount = series.Count;
      double barWidth = GroupWidth * GetMinimumXSpacing(series) / seriesCount;
      for(int i = 0; i < seriesCount; i++)
      {
        ChartSeries s = series[i];
        double offset = (i - (seriesCount - 1) / 2.0) * barWidth;
        Color color = Palette.GetColor(i);
        List<Bar> bars = [];
        for(int j = 0; j < s.Values.Length; j++)
        {
          double position = (s.X?[j] ?? j) + offset;
          bars.Add(new Bar { Position = position, Value = s.Values[j], Size = barWidth, FillColor = color, LineColor = color });
        }

        ScottPlot.Plottables.BarPlot barPlot = plot.Add.Bars(bars);
        barPlot.LegendText = s.Name ?? string.Empty;
      }

      if(categories is { Count: > 0 } && series.All(s => s.X is null))
      {
        SetCategoryTicks(plot, categories);
      }

      plot.Axes.Margins(bottom: 0);
    }

    private static void AddPie(Plot plot, ChartSeries series, IReadOnlyList<string>? categories)
    {
      double total = series.Values.Sum();
      List<PieSlice> slices = [];
      for(int i = 0; i < series.Values.Length; i++)
      {
        double value = series.Values[i];
        string name = categories is { Count: > 0 } ? categories[i] : $"#{i + 1}";
        double percent = value / total * 100;
        slices.Add(new PieSlice
        {
          Value = value,
          FillColor = Palette.GetColor(i),
          LegendText = $"{name} ({percent:0.#}%)"
        });
      }

      plot.Add.Pie(slices);
      plot.Axes.Frameless();
      plot.HideGrid();
      plot.ShowLegend(Alignment.UpperRight);
    }

    private static double GetMinimumXSpacing(IReadOnlyList<ChartSeries> series)
    {
      double[] xs = [.. series.Where(s => s.X is not null).SelectMany(s => s.X!).Distinct().Order()];
      if(xs.Length < 2)
      {
        return 1;
      }

      double minSpacing = double.MaxValue;
      for(int i = 1; i < xs.Length; i++)
      {
        minSpacing = Math.Min(minSpacing, xs[i] - xs[i - 1]);
      }

      bool hasIndexedSeries = series.Any(s => s.X is null);
      return hasIndexedSeries ? Math.Min(minSpacing, 1) : minSpacing;
    }

    private static void SetCategoryTicks(Plot plot, IReadOnlyList<string> categories)
    {
      double[] positions = Enumerable.Range(0, categories.Count).Select(x => (double)x).ToArray();
      plot.Axes.Bottom.SetTicks(positions, [.. categories]);
    }
  }

}
