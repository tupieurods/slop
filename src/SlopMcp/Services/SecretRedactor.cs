using System.Text.RegularExpressions;

namespace SlopMcp.Services {

  internal static class SecretRedactor
  {
    private static readonly Regex _secretPattern =
      new(@"secret=[^&""\\]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    internal static string Redact(string input)
      => _secretPattern.Replace(input, "secret=REDACTED");
  }

}
