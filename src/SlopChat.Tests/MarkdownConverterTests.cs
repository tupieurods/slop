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
}
