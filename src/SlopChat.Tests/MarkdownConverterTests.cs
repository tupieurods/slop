using SlopChat.Services;
using Telegram.Bot.Types.Enums;

namespace SlopChat.Tests;

public class MarkdownConverterTests
{
  [Fact]
  public void PlainText_ReturnsUnchanged()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Hello world");

    Assert.Equal("Hello world", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void EmptyString_ReturnsEmpty()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("");

    Assert.Equal("", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void EmptyCodeBlock_NoEntity()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Before\n```\n```\nAfter");

    Assert.Equal("Before\nAfter", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void Null_ReturnsEmpty()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities(null!);

    Assert.Equal("", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void Bold_DoubleAsterisks()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("This is **bold** text");

    Assert.Equal("This is bold text", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Bold, entities[0].Type);
    Assert.Equal(8, entities[0].Offset);
    Assert.Equal(4, entities[0].Length);
  }

  [Fact]
  public void Italic_SingleAsterisk()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("This is *italic* text");

    Assert.Equal("This is italic text", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Italic, entities[0].Type);
    Assert.Equal(8, entities[0].Offset);
    Assert.Equal(6, entities[0].Length);
  }

  [Fact]
  public void Italic_Underscores()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("This is _italic_ text");

    Assert.Equal("This is italic text", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Italic, entities[0].Type);
    Assert.Equal(8, entities[0].Offset);
    Assert.Equal(6, entities[0].Length);
  }

  [Fact]
  public void Strikethrough()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("This is ~~deleted~~ text");

    Assert.Equal("This is deleted text", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Strikethrough, entities[0].Type);
    Assert.Equal(8, entities[0].Offset);
    Assert.Equal(7, entities[0].Length);
  }

  [Fact]
  public void InlineCode()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Use `Console.WriteLine` here");

    Assert.Equal("Use Console.WriteLine here", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Code, entities[0].Type);
    Assert.Equal(4, entities[0].Offset);
    Assert.Equal(17, entities[0].Length);
  }

  [Fact]
  public void CodeBlock_WithLanguage()
  {
    string input = "Before\n```csharp\nvar x = 1;\n```\nAfter";
    var (text, entities) = MarkdownConverter.ToTelegramEntities(input);

    Assert.Equal("Before\nvar x = 1;\nAfter", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Pre, entities[0].Type);
    Assert.Equal("csharp", entities[0].Language);
    Assert.Equal(7, entities[0].Offset);
    Assert.Equal(10, entities[0].Length);
  }

  [Fact]
  public void CodeBlock_WithoutLanguage()
  {
    string input = "Before\n```\nvar x = 1;\n```\nAfter";
    var (text, entities) = MarkdownConverter.ToTelegramEntities(input);

    Assert.Equal("Before\nvar x = 1;\nAfter", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Pre, entities[0].Type);
    Assert.Null(entities[0].Language);
  }

  [Fact]
  public void Link()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Click [here](https://example.com) now");

    Assert.Equal("Click here now", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.TextLink, entities[0].Type);
    Assert.Equal("https://example.com", entities[0].Url);
    Assert.Equal(6, entities[0].Offset);
    Assert.Equal(4, entities[0].Length);
  }

  [Fact]
  public void Heading_Level1()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("# Title");

    Assert.Equal("Title", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Bold, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal(5, entities[0].Length);
  }

  [Fact]
  public void Heading_Level3()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("### Subtitle");

    Assert.Equal("Subtitle", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Bold, entities[0].Type);
  }

  [Fact]
  public void Heading_MidText()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Intro\n## Section\nBody");

    Assert.Equal("Intro\nSection\nBody", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Bold, entities[0].Type);
    Assert.Equal(6, entities[0].Offset);
    Assert.Equal(7, entities[0].Length);
  }

  [Fact]
  public void BulletList_Dash()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Items:\n- First\n- Second");

    Assert.Equal("Items:\n• First\n• Second", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void BulletList_Asterisk()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("* Item one\n* Item two");

    Assert.Equal("• Item one\n• Item two", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void MultipleBold_CorrectOffsets()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("**A** and **B**");

    Assert.Equal("A and B", text);
    Assert.Equal(2, entities.Count);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal(1, entities[0].Length);
    Assert.Equal(6, entities[1].Offset);
    Assert.Equal(1, entities[1].Length);
  }

  [Fact]
  public void MixedFormatting()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("**Bold** and *italic* and `code`");

    Assert.Equal("Bold and italic and code", text);
    Assert.Equal(3, entities.Count);
    Assert.Equal(MessageEntityType.Bold, entities[0].Type);
    Assert.Equal(MessageEntityType.Italic, entities[1].Type);
    Assert.Equal(MessageEntityType.Code, entities[2].Type);
  }

  [Fact]
  public void UnmatchedAsterisks_PassedThrough()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Price is 5*3 = 15");

    Assert.Equal("Price is 5*3 = 15", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void UnmatchedBackticks_PassedThrough()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Use ` for quotes");

    Assert.Equal("Use ` for quotes", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void CodeBlockPreservesMarkdown()
  {
    string input = "```\n**not bold** *not italic*\n```";
    var (text, entities) = MarkdownConverter.ToTelegramEntities(input);

    Assert.Equal("**not bold** *not italic*", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Pre, entities[0].Type);
  }

  [Fact]
  public void TypicalLlmResponse()
  {
    string input = """
                   # Ответ
                   
                   Вот что я нашел:
                   
                   **Курс доллара** на сегодня составляет *94.30* рублей.
                   
                   ```python
                   rate = get_rate("USD", "RUB")
                   ```
                   
                   - Покупка: 93.80
                   - Продажа: 94.80
                   
                   Подробнее [тут](https://cbr.ru).
                   """;

    var (text, entities) = MarkdownConverter.ToTelegramEntities(input);

    Assert.DoesNotContain("**", text);
    Assert.DoesNotContain("```", text);
    Assert.Contains("Ответ", text);
    Assert.Contains("Курс доллара", text);
    Assert.Contains("94.30", text);
    Assert.Contains("rate = get_rate", text);
    Assert.Contains("•", text);
    Assert.Contains("тут", text);

    Assert.True(entities.Count >= 5);
    Assert.Contains(entities, e => e.Type == MessageEntityType.Bold);
    Assert.Contains(entities, e => e.Type == MessageEntityType.Italic);
    Assert.Contains(entities, e => e.Type == MessageEntityType.Pre);
    Assert.Contains(entities, e => e.Type == MessageEntityType.TextLink);
  }

  [Fact]
  public void Convert_BareUrlWithUnderscores_PreservesUrlAndEmitsUrlEntity()
  {
    string url = "https://wheelfront.com/wp-content/uploads/formidable/8/Zito_ZS05_19_with_Mercedes_Benz_C_Class_W205__gallery_1.jpg";
    var (text, entities) = MarkdownConverter.ToTelegramEntities(url);

    Assert.Equal(url, text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal(url.Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UrlInSentence_PreservesUrlAndSurroundingText()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Look at https://x.com/a_b_c here.");

    Assert.Equal("Look at https://x.com/a_b_c here.", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(8, entities[0].Offset);
    Assert.Equal("https://x.com/a_b_c".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UrlWithTrailingPeriod_TrimsPeriod()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("see https://x.com/a.");

    Assert.Equal("see https://x.com/a.", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(4, entities[0].Offset);
    Assert.Equal("https://x.com/a".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UrlInParens_TrimsClosingParen()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("(https://x.com/a)");

    Assert.Equal("(https://x.com/a)", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(1, entities[0].Offset);
    Assert.Equal("https://x.com/a".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UrlWithBalancedParens_KeepsParens()
  {
    string url = "https://en.wikipedia.org/wiki/Foo_(bar)";
    var (text, entities) = MarkdownConverter.ToTelegramEntities(url);

    Assert.Equal(url, text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal(url.Length, entities[0].Length);
  }

  [Fact]
  public void Convert_MarkdownLink_StillUsesTextLink()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("[wiki](https://x.com/a_b)");

    Assert.Equal("wiki", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.TextLink, entities[0].Type);
    Assert.Equal("https://x.com/a_b", entities[0].Url);
  }

  [Fact]
  public void Convert_IntraWordUnderscore_NotItalicized()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("snake_case_var");

    Assert.Equal("snake_case_var", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void Convert_RegularUnderscoreItalic_StillWorks()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("_italic_");

    Assert.Equal("italic", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Italic, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal(6, entities[0].Length);
  }

  [Fact]
  public void Convert_MixedItalicAndUrl()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("Use _italic_ and snake_case in url https://x.com/a_b_c");

    Assert.Equal("Use italic and snake_case in url https://x.com/a_b_c", text);
    Assert.Equal(2, entities.Count);

    var italicEntity = entities[0];
    Assert.Equal(MessageEntityType.Italic, italicEntity.Type);
    Assert.Equal(4, italicEntity.Offset);
    Assert.Equal(6, italicEntity.Length);

    var urlEntity = entities[1];
    Assert.Equal(MessageEntityType.Url, urlEntity.Type);
    Assert.Equal(33, urlEntity.Offset);
    Assert.Equal("https://x.com/a_b_c".Length, urlEntity.Length);
  }

  [Fact]
  public void Convert_UrlWithDotInsideClosingParen_TrimsBoth()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("https://x.com/a.)");

    Assert.Equal("https://x.com/a.)", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal("https://x.com/a".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UppercaseHttpsScheme_StillRecognized()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("HTTPS://x.com/a_b");

    Assert.Equal("HTTPS://x.com/a_b", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal("HTTPS://x.com/a_b".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_UrlAfterNewline_Recognized()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("line1\nhttps://x.com/a_b");

    Assert.Equal("line1\nhttps://x.com/a_b", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Url, entities[0].Type);
    Assert.Equal(6, entities[0].Offset);
    Assert.Equal("https://x.com/a_b".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_TwoUrlsSeparatedBySpace_BothRecognized()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("https://a.com/x_y https://b.com/p_q");

    Assert.Equal("https://a.com/x_y https://b.com/p_q", text);
    Assert.Equal(2, entities.Count);
    Assert.All(entities, e => Assert.Equal(MessageEntityType.Url, e.Type));
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal("https://a.com/x_y".Length, entities[0].Length);
    Assert.Equal("https://a.com/x_y ".Length, entities[1].Offset);
    Assert.Equal("https://b.com/p_q".Length, entities[1].Length);
  }

  [Fact]
  public void Convert_UrlInsideInlineCode_NotLinkified()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("see `https://x.com/a_b` here");

    Assert.Equal("see https://x.com/a_b here", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Code, entities[0].Type);
    Assert.DoesNotContain(entities, e => e.Type == MessageEntityType.Url);
  }

  [Fact]
  public void Convert_IntraWordUnderscoreInItalicCandidate_PreservesText_LocksBehavior()
  {
    // Locked-in behavior: "_a_b_" produces italic on "a_b" (closing on the middle _,
    // skipping because next char is a letter, then closing on the final _).
    // The outer _ pair wraps "a_b"; the closing _ is consumed. Result text is "a_b".
    var (text, entities) = MarkdownConverter.ToTelegramEntities("_a_b_");

    Assert.Equal("a_b", text);
    Assert.Single(entities);
    Assert.Equal(MessageEntityType.Italic, entities[0].Type);
    Assert.Equal(0, entities[0].Offset);
    Assert.Equal("a_b".Length, entities[0].Length);
  }

  [Fact]
  public void Convert_AsteriskItalic_StillWorks()
  {
    var (text1, entities1) = MarkdownConverter.ToTelegramEntities("*italic*");

    Assert.Equal("italic", text1);
    Assert.Single(entities1);
    Assert.Equal(MessageEntityType.Italic, entities1[0].Type);
    Assert.Equal(0, entities1[0].Offset);
    Assert.Equal("italic".Length, entities1[0].Length);

    var (text2, entities2) = MarkdownConverter.ToTelegramEntities("un*believ*able");

    Assert.Equal("unbelievable", text2);
    Assert.Single(entities2);
    Assert.Equal(MessageEntityType.Italic, entities2[0].Type);
    Assert.Equal(2, entities2[0].Offset);
    Assert.Equal("believ".Length, entities2[0].Length);
  }

  [Fact]
  public void BulletList_Asterisk_WithBoldItem_ProducesBulletAndBold()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("List:\n* **Alpha**: first\n* **Beta**: second");

    Assert.Equal("List:\n• Alpha: first\n• Beta: second", text);
    Assert.Equal(2, entities.Count);
    Assert.All(entities, e => Assert.Equal(MessageEntityType.Bold, e.Type));
    Assert.Equal("List:\n• ".Length, entities[0].Offset);
    Assert.Equal("Alpha".Length, entities[0].Length);
    Assert.Equal("List:\n• Alpha: first\n• ".Length, entities[1].Offset);
    Assert.Equal("Beta".Length, entities[1].Length);
  }

  [Fact]
  public void ItalicAsterisk_OpenerFollowedBySpace_NotItalicized()
  {
    var (text, entities) = MarkdownConverter.ToTelegramEntities("a * b * c");

    Assert.Equal("a * b * c", text);
    Assert.Empty(entities);
  }

  [Fact]
  public void BulletList_Asterisk_MultipleBoldBullets_AllConverted()
  {
    string input =
      "Heading line\n" +
      "* **One**: alpha details\n" +
      "* **Two**: beta details\n" +
      "* **Three**: gamma details";

    var (text, entities) = MarkdownConverter.ToTelegramEntities(input);

    string expected =
      "Heading line\n" +
      "• One: alpha details\n" +
      "• Two: beta details\n" +
      "• Three: gamma details";
    Assert.Equal(expected, text);
    Assert.DoesNotContain('*', text);
    Assert.Equal(3, entities.Count);
    Assert.All(entities, e => Assert.Equal(MessageEntityType.Bold, e.Type));
    Assert.Equal("One", text.Substring(entities[0].Offset, entities[0].Length));
    Assert.Equal("Two", text.Substring(entities[1].Offset, entities[1].Length));
    Assert.Equal("Three", text.Substring(entities[2].Offset, entities[2].Length));
  }
}
