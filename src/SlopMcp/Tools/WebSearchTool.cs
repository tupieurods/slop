using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using SlopMcp.Services;

namespace SlopMcp.Tools {

  [McpServerToolType]
  public class WebSearchTool
  {
    private readonly SearXngClient _searXng;

    public WebSearchTool(SearXngClient searXng)
    {
      _searXng = searXng;
    }

    [McpServerTool(Name = "web_search"), Description("Search the internet for current information. Returns titles, URLs, and content snippets.")]
    public async Task<string> SearchAsync(
      [Description("The search query")] string query,
      [Description("Maximum number of results to return (1-10)")] int maxResults = 5,
      CancellationToken ct = default
    )
    {
      if(maxResults is < 1 or > 10)
      {
        maxResults = 5;
      }

      var results = await _searXng.SearchAsync(query, maxResults, ct);

      if(results.Count == 0)
      {
        return "No search results found.";
      }

      var sb = new StringBuilder();
      for(int i = 0; i < results.Count; i++)
      {
        var r = results[i];
        sb.AppendLine($"[{i + 1}] {r.Title}");
        sb.AppendLine($"    URL: {r.Url}");
        if(!string.IsNullOrWhiteSpace(r.Content))
        {
          sb.AppendLine($"    {r.Content}");
        }
        sb.AppendLine();
      }

      return sb.ToString();
    }
  }

}
