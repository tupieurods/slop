using System.Text.RegularExpressions;

namespace SlopChat.Services
{
  internal static partial class LlmOutputSanitizer
  {
    private const char StrayThoughtToken = '\uC810';

    public static string Sanitize(string text)
    {
      if(string.IsNullOrEmpty(text))
      {
        return text;
      }

      string result = text;
      if(result.Contains(StrayThoughtToken))
      {
        result = LeadingStrayTokenRegex().Replace(result, string.Empty);
      }

      if(result.Contains("$\\$", StringComparison.Ordinal))
      {
        result = CurrencyInMathRegex().Replace(result, m => "\\$" + TextCommandRegex().Replace(m.Groups["body"].Value, "$1"));
      }

      return result;
    }

    // A Hangul syllable right after the token means it is part of a real Korean word (e.g. \uC810\uC2EC), not a stray token.
    [GeneratedRegex(@"\A\s*(?:\uC810(?![\uAC00-\uD7A3])\s*)+")]
    private static partial Regex LeadingStrayTokenRegex();

    // Matches "$\$<body>$" on a single line; the opening "$" must not be part of "$$" or an escaped "\$",
    // and the closing "$" must not start a "$$" block.
    [GeneratedRegex(@"(?<![\\$])\$\\\$(?<body>(?:[^$\\\n]|\\\$|\\(?!\$))*?)\$(?!\$)")]
    private static partial Regex CurrencyInMathRegex();

    [GeneratedRegex(@"\\text\{([^{}]*)\}")]
    private static partial Regex TextCommandRegex();
  }
}
