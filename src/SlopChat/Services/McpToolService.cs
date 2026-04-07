using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;

namespace SlopChat.Services {

  public class McpToolService : IToolExecutor, IAsyncDisposable
  {
    private readonly string _mcpServerUrl;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpToolService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private McpClient? _client;
    private IList<McpClientTool>? _mcpTools;
    private IReadOnlyList<ChatTool>? _chatTools;
    private bool _initialized;
    private bool _disposed;

    public McpToolService(string mcpServerUrl, ILoggerFactory loggerFactory)
    {
      _mcpServerUrl = mcpServerUrl;
      _loggerFactory = loggerFactory;
      _logger = loggerFactory.CreateLogger<McpToolService>();
    }

    public async Task<IReadOnlyList<ChatTool>> GetChatToolsAsync(CancellationToken ct)
    {
      await EnsureInitializedAsync(ct);
      return _chatTools ?? [];
    }

    public async Task<string> ExecuteAsync(string toolName, BinaryData arguments, CancellationToken ct)
    {
      await EnsureInitializedAsync(ct);

      McpClientTool? mcpTool = _mcpTools?.FirstOrDefault(t => t.Name == toolName);
      if(mcpTool is null)
      {
        return $"Tool '{toolName}' not found.";
      }

      try
      {
        Dictionary<string, object?>? args =
          JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments.ToString());

        CallToolResult result = await mcpTool.CallAsync(args, cancellationToken: ct);

        var sb = new StringBuilder();
        foreach(ContentBlock block in result.Content)
        {
          if(block is TextContentBlock textBlock)
          {
            sb.AppendLine(textBlock.Text);
          }
        }

        string text = sb.ToString().TrimEnd();
        return string.IsNullOrEmpty(text) ? "Tool returned no text content." : text;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to execute MCP tool {ToolName}", toolName);
        return $"Tool execution error: {ex.Message}";
      }
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
      if(_initialized)
      {
        return;
      }

      await _initLock.WaitAsync(ct);
      try
      {
        if(_initialized)
        {
          return;
        }

        await ConnectAsync(ct);
        _initialized = true;
      }
      catch(Exception ex)
      {
        _logger.LogError(ex, "Failed to connect to MCP server at {Url}. Tools will be unavailable", _mcpServerUrl);
        _chatTools = [];
        _mcpTools = [];
        _initialized = true;
      }
      finally
      {
        _initLock.Release();
      }
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
      _logger.LogInformation("Connecting to MCP server at {Url}", _mcpServerUrl);

      var transportOptions = new HttpClientTransportOptions
      {
        Endpoint = new Uri(_mcpServerUrl)
      };

      var transport = new HttpClientTransport(transportOptions, _loggerFactory);

      _client = await McpClient.CreateAsync(
        transport,
        new McpClientOptions
        {
          ClientInfo = new Implementation { Name = "SlopChat", Version = "1.0" }
        },
        _loggerFactory,
        ct
      );

      _mcpTools = await _client.ListToolsAsync(cancellationToken: ct);

      var chatTools = new List<ChatTool>();
      foreach(McpClientTool tool in _mcpTools)
      {
        chatTools.Add(ChatTool.CreateFunctionTool(
          tool.Name,
          tool.Description,
          BinaryData.FromString(tool.JsonSchema.GetRawText())
        ));
      }

      _chatTools = chatTools;
      _logger.LogInformation("Connected to MCP server. Discovered {Count} tools", _chatTools.Count);
    }

    public async ValueTask DisposeAsync()
    {
      if(_disposed)
      {
        return;
      }

      _disposed = true;

      if(_client is not null)
      {
        await _client.DisposeAsync();
      }

      _initLock.Dispose();
    }
  }

}
