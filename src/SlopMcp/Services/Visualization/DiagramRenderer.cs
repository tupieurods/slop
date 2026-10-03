using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Core.Routing;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using SkiaSharp;
using SlopMcp.Models.Visualization;
using DrawingEdge = Microsoft.Msagl.Drawing.Edge;
using DrawingGraph = Microsoft.Msagl.Drawing.Graph;
using DrawingNode = Microsoft.Msagl.Drawing.Node;
using GeometryLabel = Microsoft.Msagl.Core.Layout.Label;

namespace SlopMcp.Services.Visualization {

  public class DiagramRenderer
  {
    public const int MaxNodes = 150;
    public const int MaxEdges = 400;
    private const float FontSize = 16f;
    private const float TitleFontSize = 26f;
    private const double NodePaddingX = 14;
    private const double NodePaddingY = 10;
    private const double EdgeLabelPadding = 4;
    private const float Margin = 30f;
    private const float MaxImageSide = 2560f;
    private const float PreferredScale = 2f;
    private const double MaxAspectRatio = 5;
    private const int CurveSamples = 64;

    private static readonly SKColor BackgroundColor = SKColors.White;
    private static readonly SKColor DefaultNodeFill = SKColor.Parse("#DCE9F7");
    private static readonly SKColor NodeStroke = SKColor.Parse("#4A6FA5");
    private static readonly SKColor EdgeColor = SKColor.Parse("#555555");
    private static readonly SKColor TextColor = SKColor.Parse("#1A1A1A");

    public static IReadOnlyList<string> SupportedShapes { get; } = ["box", "rounded", "ellipse", "diamond"];

    public byte[] Render(
      IReadOnlyList<DiagramNode> nodes,
      IReadOnlyList<DiagramEdge>? edges,
      string? direction = null,
      string? title = null
    )
    {
      edges ??= [];
      Validate(nodes, edges);
      bool leftToRight = ParseDirection(direction);

      string allText = string.Join(" ", nodes.Select(LabelOf).Concat(edges.Select(e => e.Label ?? string.Empty)).Append(title ?? string.Empty));
      SKTypeface typeface = ResolveTypeface(allText);
      using var font = new SKFont(typeface, FontSize);
      using var titleFont = new SKFont(typeface, TitleFontSize) { Embolden = true };

      var graph = new DrawingGraph();
      foreach(DiagramNode node in nodes)
      {
        DrawingNode drawingNode = graph.AddNode(node.Id);
        drawingNode.LabelText = LabelOf(node);
      }

      List<DrawingEdge> drawingEdges = [];
      foreach(DiagramEdge edge in edges)
      {
        DrawingEdge drawingEdge = string.IsNullOrWhiteSpace(edge.Label)
          ? graph.AddEdge(edge.From, edge.To)
          : graph.AddEdge(edge.From, edge.Label, edge.To);
        drawingEdges.Add(drawingEdge);
      }

      GeometryGraph geometryGraph = graph.CreateGeometryGraph();

      foreach(DiagramNode node in nodes)
      {
        DrawingNode drawingNode = graph.FindNode(node.Id);
        (double width, double height) = MeasureLines(font, SplitLines(LabelOf(node)));
        drawingNode.GeometryNode.BoundaryCurve = CreateBoundary(NormalizeShape(node.Shape), width + 2 * NodePaddingX, height + 2 * NodePaddingY);
      }

      foreach(DrawingEdge drawingEdge in drawingEdges)
      {
        GeometryLabel? label = drawingEdge.Label?.GeometryLabel;
        if(label is null || string.IsNullOrEmpty(drawingEdge.LabelText))
        {
          continue;
        }

        (double width, double height) = MeasureLines(font, SplitLines(drawingEdge.LabelText));
        label.Width = width + 2 * EdgeLabelPadding;
        label.Height = height + 2 * EdgeLabelPadding;
      }

      var settings = new SugiyamaLayoutSettings
      {
        NodeSeparation = 30,
        LayerSeparation = 50
      };
      settings.EdgeRoutingSettings.EdgeRoutingMode = EdgeRoutingMode.SugiyamaSplines;
      if(leftToRight)
      {
        settings.Transformation = PlaneTransformation.Rotation(Math.PI / 2);
      }

      LayoutHelpers.CalculateLayout(geometryGraph, settings, null);
      geometryGraph.UpdateBoundingBox();

      return Draw(nodes, graph, drawingEdges, geometryGraph.BoundingBox, font, titleFont, title);
    }

    private static void Validate(IReadOnlyList<DiagramNode>? nodes, IReadOnlyList<DiagramEdge> edges)
    {
      if(nodes is null || nodes.Count == 0)
      {
        throw new ArgumentException("At least one node is required.", nameof(nodes));
      }

      if(nodes.Count > MaxNodes)
      {
        throw new ArgumentException($"Too many nodes ({nodes.Count}); maximum is {MaxNodes}.", nameof(nodes));
      }

      if(edges.Count > MaxEdges)
      {
        throw new ArgumentException($"Too many edges ({edges.Count}); maximum is {MaxEdges}.", nameof(edges));
      }

      HashSet<string> ids = new(StringComparer.Ordinal);
      foreach(DiagramNode node in nodes)
      {
        if(string.IsNullOrWhiteSpace(node.Id))
        {
          throw new ArgumentException("Every node must have a non-empty id.", nameof(nodes));
        }

        if(!ids.Add(node.Id))
        {
          throw new ArgumentException($"Duplicate node id '{node.Id}'.", nameof(nodes));
        }

        if(node.Shape is not null && !SupportedShapes.Contains(NormalizeShape(node.Shape)))
        {
          throw new ArgumentException($"Unknown shape '{node.Shape}' for node '{node.Id}'. Supported shapes: {string.Join(", ", SupportedShapes)}.", nameof(nodes));
        }

        if(node.Color is not null && !SKColor.TryParse(node.Color, out _))
        {
          throw new ArgumentException($"Invalid color '{node.Color}' for node '{node.Id}'. Use hex like #FFCC00.", nameof(nodes));
        }
      }

      foreach(DiagramEdge edge in edges)
      {
        if(!ids.Contains(edge.From))
        {
          throw new ArgumentException($"Edge references unknown node '{edge.From}' (from).", nameof(edges));
        }

        if(!ids.Contains(edge.To))
        {
          throw new ArgumentException($"Edge references unknown node '{edge.To}' (to).", nameof(edges));
        }
      }
    }

    private static bool ParseDirection(string? direction)
    {
      string normalized = (direction ?? "TB").Trim().ToUpperInvariant();
      return normalized switch
      {
        "TB" or "TD" or "" => false,
        "LR" => true,
        _ => throw new ArgumentException($"Unknown direction '{direction}'. Supported: TB, LR.", nameof(direction))
      };
    }

    private static string NormalizeShape(string? shape) => string.IsNullOrWhiteSpace(shape) ? "box" : shape.Trim().ToLowerInvariant();

    private static string LabelOf(DiagramNode node) => string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label;

    private static string[] SplitLines(string text) => text.Replace("\\n", "\n").Replace("\r", string.Empty).Split('\n');

    private static SKTypeface ResolveTypeface(string text)
    {
      string family = ScottPlot.Fonts.Detect(text);
      return ScottPlot.Fonts.GetTypeface(family, false, false);
    }

    private static (double Width, double Height) MeasureLines(SKFont font, string[] lines)
    {
      double width = lines.Max(line => font.MeasureText(line));
      double height = lines.Length * font.Spacing;
      return (Math.Max(width, 1), height);
    }

    private static ICurve CreateBoundary(string shape, double width, double height)
    {
      var center = new Point(0, 0);
      return shape switch
      {
        "rounded" => CurveFactory.CreateRectangleWithRoundedCorners(width, height, 10, 10, center),
        "ellipse" => CurveFactory.CreateEllipse(width * 0.72, height * 0.72, center),
        "diamond" => CurveFactory.CreateDiamond(width * 0.8, height * 0.85, center),
        _ => CurveFactory.CreateRectangle(width, height, center)
      };
    }

    private static byte[] Draw(
      IReadOnlyList<DiagramNode> nodes,
      DrawingGraph graph,
      IReadOnlyList<DrawingEdge> edges,
      Rectangle bounds,
      SKFont font,
      SKFont titleFont,
      string? title
    )
    {
      float titleHeight = string.IsNullOrWhiteSpace(title) ? 0 : titleFont.Spacing + 10;
      float titleWidth = string.IsNullOrWhiteSpace(title) ? 0 : titleFont.MeasureText(title);
      double contentWidth = Math.Max(bounds.Width, titleWidth);
      double contentLogicalWidth = contentWidth + 2 * Margin;
      double contentLogicalHeight = bounds.Height + 2 * Margin + titleHeight;
      double logicalWidth = Math.Max(contentLogicalWidth, contentLogicalHeight / MaxAspectRatio);
      double logicalHeight = Math.Max(contentLogicalHeight, contentLogicalWidth / MaxAspectRatio);
      float scale = (float)Math.Min(PreferredScale, MaxImageSide / Math.Max(logicalWidth, logicalHeight));
      int pixelWidth = Math.Max(1, (int)Math.Ceiling(logicalWidth * scale));
      int pixelHeight = Math.Max(1, (int)Math.Ceiling(logicalHeight * scale));
      float padX = (float)((logicalWidth - contentLogicalWidth) / 2);
      float padY = (float)((logicalHeight - contentLogicalHeight) / 2);
      float offsetX = (float)((contentWidth - bounds.Width) / 2) + Margin + padX;
      float offsetY = Margin + titleHeight + padY;

      SKPoint Map(Point p) => new((float)(p.X - bounds.Left) + offsetX, (float)(bounds.Top - p.Y) + offsetY);

      using SKSurface surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight));
      SKCanvas canvas = surface.Canvas;
      canvas.Clear(BackgroundColor);
      canvas.Scale(scale);

      using var textPaint = new SKPaint { Color = TextColor, IsAntialias = true };
      using var edgePaint = new SKPaint { Color = EdgeColor, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f };
      using var arrowPaint = new SKPaint { Color = EdgeColor, IsAntialias = true, Style = SKPaintStyle.Fill };
      using var nodeFill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
      using var nodeStroke = new SKPaint { Color = NodeStroke, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.6f };

      if(!string.IsNullOrWhiteSpace(title))
      {
        canvas.DrawText(title, (float)(logicalWidth / 2), Margin + padY + titleFont.Size * 0.9f, SKTextAlign.Center, titleFont, textPaint);
      }

      foreach(DrawingEdge edge in edges)
      {
        Edge geometryEdge = edge.GeometryEdge;
        if(geometryEdge.Curve is null)
        {
          continue;
        }

        using SKPath path = BuildPath(geometryEdge.Curve, Map, false);
        canvas.DrawPath(path, edgePaint);

        Arrowhead? arrowhead = geometryEdge.EdgeGeometry.TargetArrowhead;
        if(arrowhead is not null)
        {
          DrawArrowhead(canvas, Map(geometryEdge.Curve.End), Map(arrowhead.TipPosition), arrowPaint);
        }

        GeometryLabel? label = edge.Label?.GeometryLabel;
        if(label is not null && !string.IsNullOrEmpty(edge.LabelText))
        {
          SKPoint center = Map(label.Center);
          DrawCenteredLines(canvas, SplitLines(edge.LabelText), center, font, textPaint);
        }
      }

      foreach(DiagramNode node in nodes)
      {
        Node geometryNode = graph.FindNode(node.Id).GeometryNode;
        nodeFill.Color = node.Color is not null && SKColor.TryParse(node.Color, out SKColor fill) ? fill : DefaultNodeFill;
        using SKPath path = BuildPath(geometryNode.BoundaryCurve, Map, true);
        canvas.DrawPath(path, nodeFill);
        canvas.DrawPath(path, nodeStroke);
        DrawCenteredLines(canvas, SplitLines(LabelOf(node)), Map(geometryNode.Center), font, textPaint);
      }

      canvas.Flush();
      using SKImage image = surface.Snapshot();
      using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
      return data.ToArray();
    }

    private static SKPath BuildPath(ICurve curve, Func<Point, SKPoint> map, bool close)
    {
      var path = new SKPath();
      bool first = true;
      foreach(Point point in SampleCurve(curve))
      {
        SKPoint mapped = map(point);
        if(first)
        {
          path.MoveTo(mapped);
          first = false;
        }
        else
        {
          path.LineTo(mapped);
        }
      }

      if(close)
      {
        path.Close();
      }

      return path;
    }

    private static IEnumerable<Point> SampleCurve(ICurve curve)
    {
      switch(curve)
      {
        case Curve composite:
          foreach(ICurve segment in composite.Segments)
          {
            foreach(Point point in SampleCurve(segment))
            {
              yield return point;
            }
          }
          break;
        case LineSegment line:
          yield return line.Start;
          yield return line.End;
          break;
        case Polyline polyline:
          foreach(PolylinePoint polylinePoint in polyline.PolylinePoints)
          {
            yield return polylinePoint.Point;
          }

          if(polyline.Closed)
          {
            yield return polyline.StartPoint.Point;
          }
          break;
        default:
          for(int i = 0; i <= CurveSamples; i++)
          {
            double t = curve.ParStart + (curve.ParEnd - curve.ParStart) * i / CurveSamples;
            yield return curve[t];
          }
          break;
      }
    }

    private static void DrawArrowhead(SKCanvas canvas, SKPoint baseCenter, SKPoint tip, SKPaint paint)
    {
      float dx = tip.X - baseCenter.X;
      float dy = tip.Y - baseCenter.Y;
      float length = MathF.Sqrt(dx * dx + dy * dy);
      if(length < 0.01f)
      {
        return;
      }

      float halfWidth = Math.Max(length * 0.45f, 3.5f);
      float nx = -dy / length * halfWidth;
      float ny = dx / length * halfWidth;
      using var path = new SKPath();
      path.MoveTo(tip);
      path.LineTo(baseCenter.X + nx, baseCenter.Y + ny);
      path.LineTo(baseCenter.X - nx, baseCenter.Y - ny);
      path.Close();
      canvas.DrawPath(path, paint);
    }

    private static void DrawCenteredLines(SKCanvas canvas, string[] lines, SKPoint center, SKFont font, SKPaint paint)
    {
      SKFontMetrics metrics = font.Metrics;
      float lineHeight = font.Spacing;
      float top = center.Y - lineHeight * lines.Length / 2;
      for(int i = 0; i < lines.Length; i++)
      {
        float baseline = top + i * lineHeight - metrics.Ascent;
        canvas.DrawText(lines[i], center.X, baseline, SKTextAlign.Center, font, paint);
      }
    }
  }

}
