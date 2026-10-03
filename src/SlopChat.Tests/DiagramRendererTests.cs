using SlopMcp.Models.Visualization;
using SlopMcp.Services.Visualization;

namespace SlopChat.Tests
{
  public class DiagramRendererTests
  {
    private readonly DiagramRenderer _renderer = new();

    private static readonly DiagramNode[] Nodes =
    [
      new DiagramNode { Id = "build", Label = "Build" },
      new DiagramNode { Id = "test", Label = "Test\nunit + integration", Shape = "rounded" },
      new DiagramNode { Id = "ok", Label = "Passed?", Shape = "diamond", Color = "#FFE08A" }
    ];

    private static readonly DiagramEdge[] Edges =
    [
      new DiagramEdge { From = "build", To = "test" },
      new DiagramEdge { From = "test", To = "ok", Label = "results" }
    ];

    [Theory]
    [InlineData("TB")]
    [InlineData("LR")]
    [InlineData(null)]
    public void Render_SmallGraph_ReturnsPng(string? direction)
    {
      byte[] png = _renderer.Render(Nodes, Edges, direction, "Pipeline");

      Assert.True(PngAssert.IsPng(png));
    }

    [Fact]
    public void Render_NodesWithoutEdges_ReturnsPng()
    {
      byte[] png = _renderer.Render([new DiagramNode { Id = "single", Shape = "ellipse" }], null);

      Assert.True(PngAssert.IsPng(png));
    }

    [Fact]
    public void Render_LongChain_KeepsAspectRatioWithinLimit()
    {
      DiagramNode[] nodes = [.. Enumerable.Range(0, 40).Select(i => new DiagramNode { Id = $"n{i}" })];
      DiagramEdge[] edges = [.. Enumerable.Range(0, 39).Select(i => new DiagramEdge { From = $"n{i}", To = $"n{i + 1}" })];

      byte[] png = _renderer.Render(nodes, edges, "LR");

      using SkiaSharp.SKBitmap bitmap = SkiaSharp.SKBitmap.Decode(png);
      double ratio = Math.Max(bitmap.Width, bitmap.Height) / (double)Math.Min(bitmap.Width, bitmap.Height);
      Assert.True(ratio <= 5.01, $"Aspect ratio {ratio} exceeds limit");
    }

    [Fact]
    public void Render_EdgeToUnknownNode_Throws()
    {
      DiagramEdge[] edges = [new DiagramEdge { From = "build", To = "deploy" }];

      var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(Nodes, edges));

      Assert.Contains("unknown node 'deploy'", ex.Message);
    }

    [Fact]
    public void Render_DuplicateNodeId_Throws()
    {
      DiagramNode[] nodes = [new DiagramNode { Id = "a" }, new DiagramNode { Id = "a" }];

      var ex = Assert.Throws<ArgumentException>(() => _renderer.Render(nodes, null));

      Assert.Contains("Duplicate node id", ex.Message);
    }

    [Fact]
    public void Render_UnknownShape_Throws()
    {
      Assert.Throws<ArgumentException>(() => _renderer.Render([new DiagramNode { Id = "a", Shape = "star" }], null));
    }

    [Fact]
    public void Render_UnknownDirection_Throws()
    {
      Assert.Throws<ArgumentException>(() => _renderer.Render(Nodes, Edges, "XY"));
    }
  }
}
