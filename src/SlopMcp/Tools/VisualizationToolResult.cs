using ModelContextProtocol.Protocol;

namespace SlopMcp.Tools {

  internal static class VisualizationToolResult
  {
    private const string PngMimeType = "image/png";

    public static IEnumerable<ContentBlock> Image(byte[] png, string text) =>
      [ImageContentBlock.FromBytes(png, PngMimeType), new TextContentBlock { Text = text }];

    public static IEnumerable<ContentBlock> Error(string text) => [new TextContentBlock { Text = text }];
  }

}
