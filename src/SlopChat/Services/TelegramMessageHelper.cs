using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Services
{
  public static class TelegramMessageHelper
  {
    private const int MaxMessageLength = 4096;

    public static async Task SendChunkedAsync(ITelegramBotClient bot, long chatId, string text, int replyToMessageId, CancellationToken ct)
    {
      var (plainText, entities) = MarkdownConverter.ToTelegramEntities(text);

      if(plainText.Length <= MaxMessageLength)
      {
        await bot.SendMessage(chatId, plainText,
          entities: entities,
          replyParameters: new ReplyParameters { MessageId = replyToMessageId },
          cancellationToken: ct);
        return;
      }

      int offset = 0;
      bool isFirst = true;
      while(offset < plainText.Length)
      {
        int length = Math.Min(MaxMessageLength, plainText.Length - offset);

        if(offset + length < plainText.Length)
        {
          int newlineIndex = plainText.LastIndexOf('\n', offset + length - 1, length);
          if(newlineIndex > offset)
          {
            length = newlineIndex - offset + 1;
          }
        }

        string chunk = plainText.Substring(offset, length);
        var chunkEntities = SliceEntities(entities, offset, length);
        ReplyParameters? replyParams = isFirst ? new ReplyParameters { MessageId = replyToMessageId } : null;
        await bot.SendMessage(chatId, chunk,
          entities: chunkEntities.Count > 0 ? chunkEntities : null,
          replyParameters: replyParams,
          cancellationToken: ct);

        offset += length;
        isFirst = false;
      }
    }

    private static List<MessageEntity> SliceEntities(List<MessageEntity> entities, int offset, int length)
    {
      var result = new List<MessageEntity>();
      int end = offset + length;

      foreach(MessageEntity entity in entities)
      {
        int entityEnd = entity.Offset + entity.Length;

        if(entity.Offset >= offset && entityEnd <= end)
        {
          result.Add(new MessageEntity
          {
            Type = entity.Type,
            Offset = entity.Offset - offset,
            Length = entity.Length,
            Url = entity.Url,
            Language = entity.Language
          });
        }
      }

      return result;
    }
  }
}
