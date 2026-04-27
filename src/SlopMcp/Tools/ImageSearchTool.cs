using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using SlopMcp.Services;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class ImageSearchTool
  {
    private readonly SearXngClient _searXng;
    private readonly ImageUrlValidator _validator;

    public ImageSearchTool(SearXngClient searXng, ImageUrlValidator validator)
    {
      _searXng = searXng;
      _validator = validator;
    }

    [McpServerTool(Name = "image_search"), Description("Search for real, validated image URLs on the internet. Use this whenever the user asks for a picture/photo/image of something. Returns titles, image URLs, source page URLs, and thumbnails.")]
    public async Task<string> SearchAsync(
      [Description("The image search query")] string query,
      [Description("Maximum number of results to return (1-10)")] int maxResults = 3,
      CancellationToken ct = default
    )
    {
      if(maxResults is < 1 or > 10)
      {
        maxResults = 3;
      }

      int fetchCount = Math.Min(maxResults * 3, 30);
      var candidates = await _searXng.SearchImagesAsync(query, fetchCount, ct);

      var imageUrls = candidates.Select(c => c.ImageUrl).ToList();
      var validUrls = await _validator.ValidateAsync(imageUrls, ct);
      var validSet = new HashSet<string>(validUrls, StringComparer.Ordinal);

      var results = candidates
        .Where(c => validSet.Contains(c.ImageUrl))
        .Take(maxResults)
        .ToList();

      if(results.Count == 0)
      {
        return "No valid image results found.";
      }

      var sb = new StringBuilder();
      for(int i = 0; i < results.Count; i++)
      {
        var r = results[i];
        sb.AppendLine($"[{i + 1}] {r.Title}");
        sb.AppendLine($"    Image URL: {r.ImageUrl}");
        sb.AppendLine($"    Source: {r.SourceUrl}");
        if(!string.IsNullOrWhiteSpace(r.ThumbnailUrl))
        {
          sb.AppendLine($"    Thumbnail: {r.ThumbnailUrl}");
        }
        if(!string.IsNullOrWhiteSpace(r.Resolution))
        {
          sb.AppendLine($"    Resolution: {r.Resolution}");
        }
        sb.AppendLine();
      }

      return sb.ToString();
    }
  }

}
