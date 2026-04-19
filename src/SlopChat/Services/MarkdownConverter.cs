using System.Text;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SlopChat.Services;

public static class MarkdownConverter
{
  public static (string PlainText, List<MessageEntity> Entities) ToTelegramEntities(string markdown)
  {
    if(string.IsNullOrEmpty(markdown))
    {
      return (string.Empty, []);
    }

    var sb = new StringBuilder(markdown.Length);
    var entities = new List<MessageEntity>();
    int pos = 0;

    while(pos < markdown.Length)
    {
      int? next = TryFencedCodeBlock(markdown, pos, sb, entities)
        ?? TryInlineCode(markdown, pos, sb, entities)
        ?? TryLink(markdown, pos, sb, entities)
        ?? TryHeading(markdown, pos, sb, entities)
        ?? TryBold(markdown, pos, sb, entities)
        ?? TryStrikethrough(markdown, pos, sb, entities)
        ?? TryItalicAsterisk(markdown, pos, sb, entities)
        ?? TryItalicUnderscore(markdown, pos, sb, entities)
        ?? TryBulletList(markdown, pos, sb);

      if(next.HasValue)
      {
        pos = next.Value;
      }
      else
      {
        sb.Append(markdown[pos]);
        pos++;
      }
    }

    return (sb.ToString(), entities);
  }

  private static int? TryFencedCodeBlock(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(pos + 2 >= text.Length || text[pos] != '`' || text[pos + 1] != '`' || text[pos + 2] != '`')
    {
      return null;
    }

    int afterTicks = pos + 3;
    int langEnd = text.IndexOf('\n', afterTicks);
    if(langEnd < 0)
    {
      langEnd = afterTicks;
    }

    string language = text[afterTicks..langEnd].Trim();
    int codeStart = langEnd < text.Length ? langEnd + 1 : langEnd;
    int closeIndex = text.IndexOf("```", codeStart, StringComparison.Ordinal);
    if(closeIndex < 0)
    {
      return null;
    }

    string code = text[codeStart..closeIndex];
    if(code.EndsWith('\n'))
    {
      code = code[..^1];
    }

    int newPos = closeIndex + 3;
    if(newPos < text.Length && text[newPos] == '\n')
    {
      newPos++;
    }

    if(code.Length == 0)
    {
      return newPos;
    }

    int entityOffset = sb.Length;
    sb.Append(code);

    var entity = new MessageEntity
    {
      Type = MessageEntityType.Pre,
      Offset = entityOffset,
      Length = code.Length
    };

    if(!string.IsNullOrEmpty(language))
    {
      entity.Language = language;
    }

    entities.Add(entity);
    // Preserve linebreak after code block in output
    if(newPos > closeIndex + 3)
    {
      sb.Append('\n');
    }

    return newPos;
  }

  private static int? TryInlineCode(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(text[pos] != '`')
    {
      return null;
    }

    int closeIndex = text.IndexOf('`', pos + 1);
    if(closeIndex <= pos + 1)
    {
      return null;
    }

    string code = text[(pos + 1)..closeIndex];
    int entityOffset = sb.Length;
    sb.Append(code);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Code,
      Offset = entityOffset,
      Length = code.Length
    });

    return closeIndex + 1;
  }

  private static int? TryLink(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(text[pos] != '[')
    {
      return null;
    }

    int closeBracket = text.IndexOf(']', pos + 1);
    if(closeBracket <= pos + 1 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
    {
      return null;
    }

    int closeParen = text.IndexOf(')', closeBracket + 2);
    if(closeParen <= closeBracket + 2)
    {
      return null;
    }

    string linkText = text[(pos + 1)..closeBracket];
    string url = text[(closeBracket + 2)..closeParen];
    int entityOffset = sb.Length;
    sb.Append(linkText);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.TextLink,
      Offset = entityOffset,
      Length = linkText.Length,
      Url = url
    });

    return closeParen + 1;
  }

  private static int? TryHeading(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(text[pos] != '#' || (pos > 0 && text[pos - 1] != '\n'))
    {
      return null;
    }

    int hashEnd = pos;
    while(hashEnd < text.Length && text[hashEnd] == '#')
    {
      hashEnd++;
    }

    if(hashEnd >= text.Length || text[hashEnd] != ' ')
    {
      return null;
    }

    int lineEnd = text.IndexOf('\n', hashEnd + 1);
    if(lineEnd < 0)
    {
      lineEnd = text.Length;
    }

    string headingText = text[(hashEnd + 1)..lineEnd];
    int entityOffset = sb.Length;
    sb.Append(headingText);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Bold,
      Offset = entityOffset,
      Length = headingText.Length
    });

    return lineEnd;
  }

  private static int? TryBold(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(pos + 1 >= text.Length || text[pos] != '*' || text[pos + 1] != '*')
    {
      return null;
    }

    int closeIndex = text.IndexOf("**", pos + 2, StringComparison.Ordinal);
    if(closeIndex <= pos + 2)
    {
      return null;
    }

    string inner = text[(pos + 2)..closeIndex];
    int entityOffset = sb.Length;
    sb.Append(inner);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Bold,
      Offset = entityOffset,
      Length = inner.Length
    });

    return closeIndex + 2;
  }

  private static int? TryStrikethrough(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(pos + 1 >= text.Length || text[pos] != '~' || text[pos + 1] != '~')
    {
      return null;
    }

    int closeIndex = text.IndexOf("~~", pos + 2, StringComparison.Ordinal);
    if(closeIndex <= pos + 2)
    {
      return null;
    }

    string inner = text[(pos + 2)..closeIndex];
    int entityOffset = sb.Length;
    sb.Append(inner);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Strikethrough,
      Offset = entityOffset,
      Length = inner.Length
    });

    return closeIndex + 2;
  }

  private static int? TryItalicAsterisk(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(text[pos] != '*')
    {
      return null;
    }

    int closeIndex = FindClosingMarker(text, pos + 1, '*');
    if(closeIndex <= pos + 1)
    {
      return null;
    }

    string inner = text[(pos + 1)..closeIndex];
    int entityOffset = sb.Length;
    sb.Append(inner);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Italic,
      Offset = entityOffset,
      Length = inner.Length
    });

    return closeIndex + 1;
  }

  private static int? TryItalicUnderscore(string text, int pos, StringBuilder sb, List<MessageEntity> entities)
  {
    if(text[pos] != '_' || (pos + 1 < text.Length && text[pos + 1] == '_'))
    {
      return null;
    }

    int closeIndex = FindClosingMarker(text, pos + 1, '_');
    if(closeIndex <= pos + 1 || text[closeIndex - 1] == '_')
    {
      return null;
    }

    string inner = text[(pos + 1)..closeIndex];
    int entityOffset = sb.Length;
    sb.Append(inner);
    entities.Add(new MessageEntity
    {
      Type = MessageEntityType.Italic,
      Offset = entityOffset,
      Length = inner.Length
    });

    return closeIndex + 1;
  }

  private static int? TryBulletList(string text, int pos, StringBuilder sb)
  {
    if(text[pos] != '-' && text[pos] != '*')
    {
      return null;
    }

    if(pos > 0 && text[pos - 1] != '\n')
    {
      return null;
    }

    if(pos + 1 >= text.Length || text[pos + 1] != ' ')
    {
      return null;
    }

    sb.Append('•');
    return pos + 1;
  }

  private static int FindClosingMarker(string text, int start, char marker)
  {
    for(int i = start; i < text.Length; i++)
    {
      if(text[i] == '\n')
      {
        return -1;
      }

      if(text[i] == marker && i > start && text[i - 1] != ' ')
      {
        return i;
      }
    }

    return -1;
  }
}
