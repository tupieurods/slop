using System.Net;

namespace SlopMcp.Services {

  public record SearXngSearchOutcome<T>
  {
    public IReadOnlyList<T> Results { get; internal init; } = [];
    public HttpStatusCode? HttpStatus { get; internal init; }
    public string? TransportError { get; internal init; }
    public IReadOnlyList<UnresponsiveEngine> UnresponsiveEngines { get; internal init; } = [];
  }

}
