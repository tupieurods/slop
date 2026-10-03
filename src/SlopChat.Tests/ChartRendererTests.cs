using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopChat.Tests
{
  public class ChartRendererTests
  {
    private readonly ChartRenderer _renderer = new();

    [Theory]
    [InlineData("line")]
    [InlineData("bar")]
    [InlineData("scatter")]
    [InlineData("BAR")]
    public void Render_MultiSeriesChart_ReturnsPng(string type)
    {
      ChartSeries[] series =
      [
        new ChartSeries { Name = "2024", Values = [1, 3, 2, 5] },
        new ChartSeries { Name = "2025", Values = [2, 4, 3, 6] }
      ];

      byte[] png = _renderer.Render(type, series, "Title", "X", "Y", ["Q1", "Q2", "Q3", "Q4"]);

      Assert.True(PngAssert.IsPng(png));
    }

    [Fact]
    public void Render_Pie_ReturnsPng()
    {
      byte[] png = _renderer.Render("pie", [new ChartSeries { Values = [40, 35, 25] }], "Share", categories: ["A", "B", "C"]);

      Assert.True(PngAssert.IsPng(png));
    }

    [Fact]
    public void Render_ScatterWithX_ReturnsPng()
    {
      byte[] png = _renderer.Render("scatter", [new ChartSeries { Name = "pts", Values = [1, 4, 9], X = [1, 2, 3] }]);

      Assert.True(PngAssert.IsPng(png));
    }

    [Fact]
    public void Render_UnknownType_Throws()
    {
      var ex = Assert.Throws<ArgumentException>(() => _renderer.Render("radar", [new ChartSeries { Values = [1] }]));

      Assert.Contains("Unknown chart type", ex.Message);
    }

    [Fact]
    public void Render_EmptySeries_Throws()
    {
      Assert.Throws<ArgumentException>(() => _renderer.Render("line", []));
    }

    [Fact]
    public void Render_SeriesWithoutValues_Throws()
    {
      var ex = Assert.Throws<ArgumentException>(() => _renderer.Render("line", [new ChartSeries { Name = "s", Values = [] }]));

      Assert.Contains("no values", ex.Message);
    }

    [Fact]
    public void Render_MismatchedXLength_Throws()
    {
      var ex = Assert.Throws<ArgumentException>(() => _renderer.Render("line", [new ChartSeries { Values = [1, 2, 3], X = [1, 2] }]));

      Assert.Contains("X has 2 values", ex.Message);
    }

    [Fact]
    public void Render_MismatchedCategories_Throws()
    {
      Assert.Throws<ArgumentException>(() => _renderer.Render("bar", [new ChartSeries { Values = [1, 2, 3] }], categories: ["a", "b"]));
    }

    [Fact]
    public void Render_PieWithMismatchedCategories_Throws()
    {
      ChartSeries[] series = [new ChartSeries { Values = [1, 2, 3], X = [1, 2, 3] }];

      Assert.Throws<ArgumentException>(() => _renderer.Render("pie", series, categories: ["a", "b"]));
    }

    [Fact]
    public void Render_LineWithXAndMismatchedCategories_Throws()
    {
      ChartSeries[] series = [new ChartSeries { Values = [1, 2, 3], X = [1, 2, 3] }];

      Assert.Throws<ArgumentException>(() => _renderer.Render("line", series, categories: ["a"]));
    }

    [Fact]
    public void Render_BarWithNumericX_ReturnsPng()
    {
      ChartSeries[] series = [new ChartSeries { Values = [5, 7, 6], X = [2000, 2010, 2020] }];

      Assert.True(PngAssert.IsPng(_renderer.Render("bar", series)));
    }

    [Fact]
    public void Render_PieWithMultipleSeries_Throws()
    {
      ChartSeries[] series = [new ChartSeries { Values = [1, 2] }, new ChartSeries { Values = [3, 4] }];

      Assert.Throws<ArgumentException>(() => _renderer.Render("pie", series));
    }
  }
}
