using SlopChat.Services;

namespace SlopChat.Tests
{
  public class LlmOutputSanitizerTests
  {
    [Theory]
    [InlineData("점У **Бри**", "У **Бри**")]
    [InlineData("점Thought\nThinking Process:", "Thought\nThinking Process:")]
    [InlineData("  점 Привет", "Привет")]
    [InlineData("점 점Привет", "Привет")]
    [InlineData("Привет 점", "Привет 점")]
    [InlineData("점심은 맛있었어", "점심은 맛있었어")]
    [InlineData("점수", "점수")]
    public void Sanitize_StripsLeadingStrayThoughtToken(string input, string expected)
    {
      Assert.Equal(expected, LlmOutputSanitizer.Sanitize(input));
    }

    [Theory]
    [InlineData(@"около **$\$0.056$**!", @"около **\$0.056**!")]
    [InlineData(@"$\$0.04 - \$0.043$ за", @"\$0.04 - \$0.043 за")]
    [InlineData(@"$\$2.5\text{ млн}$", @"\$2.5 млн")]
    [InlineData(@"от $\$1$ до $\$2$", @"от \$1 до \$2")]
    [InlineData(@"цена $\$5$, формула $x^2$", @"цена \$5, формула $x^2$")]
    public void Sanitize_UnwrapsCurrencyFromInlineMath(string input, string expected)
    {
      Assert.Equal(expected, LlmOutputSanitizer.Sanitize(input));
    }

    [Fact]
    public void Sanitize_UnclosedCurrencyFollowedByFormula_PairsWithFirstDollarOfFormula()
    {
      Assert.Equal(@"\$5 и x$ тоже", LlmOutputSanitizer.Sanitize(@"$\$5 и $x$ тоже"));
    }

    [Theory]
    [InlineData("$x^2 + y^2$")]
    [InlineData("$$E=mc^2$$")]
    [InlineData(@"$$\$5 + x$$")]
    [InlineData(@"стоит \$5, а $x$ равен 2")]
    [InlineData("Обычный текст без формул")]
    [InlineData("")]
    public void Sanitize_LeavesOtherTextUnchanged(string input)
    {
      Assert.Equal(input, LlmOutputSanitizer.Sanitize(input));
    }
  }
}
