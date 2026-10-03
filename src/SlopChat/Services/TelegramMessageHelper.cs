using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace SlopChat.Services
{
  public static class TelegramMessageHelper
  {
    private const int MaxMessageLength = 4096;
    private const int MaxRichMessageLength = 30_000;

    public static async Task SendRichAsync(
      ITelegramBotClient bot,
      long chatId,
      string markdown,
      int replyToMessageId,
      ILogger logger,
      CancellationToken ct
    )
    {
      if(string.IsNullOrWhiteSpace(markdown))
      {
        return;
      }

      var chunks = RichMarkdownSplitter.Split(markdown, MaxRichMessageLength);
      bool isFirst = true;

      for(int i = 0; i < chunks.Count; i++)
      {
        if(string.IsNullOrWhiteSpace(chunks[i]))
        {
          continue;
        }

        ReplyParameters? replyParams = isFirst ? new ReplyParameters { MessageId = replyToMessageId } : null;
        try
        {
          await bot.SendRichMessage(
            chatId,
            new InputRichMessage { Markdown = chunks[i] },
            replyParams,
            cancellationToken: ct
          );
        }
        catch(ApiRequestException ex) when(IsRichMarkdownRejection(ex))
        {
          logger.LogWarning(
            ex,
            "Rich message chunk {ChunkIndex}/{ChunkCount} rejected in chat {ChatId}, falling back to entity formatting",
            i + 1,
            chunks.Count,
            chatId
          );
          await SendChunkedCoreAsync(bot, chatId, string.Concat(chunks.Skip(i)), isFirst ? replyToMessageId : null, ct);
          return;
        }

        isFirst = false;
      }
    }

    public static Task SendChunkedAsync(ITelegramBotClient bot, long chatId, string text, int replyToMessageId, CancellationToken ct)
      => SendChunkedCoreAsync(bot, chatId, text, replyToMessageId, ct);

    private static bool IsRichMarkdownRejection(ApiRequestException ex)
      => ex.ErrorCode == 400 && !ex.Message.Contains("message to be replied not found", StringComparison.OrdinalIgnoreCase);

    private static async Task SendChunkedCoreAsync(ITelegramBotClient bot, long chatId, string text, int? replyToMessageId, CancellationToken ct)
    {
      var (plainText, entities) = MarkdownConverter.ToTelegramEntities(text);

      if(plainText.Length <= MaxMessageLength)
      {
        await bot.SendMessage(chatId, plainText,
          entities: entities,
          replyParameters: BuildReplyParameters(replyToMessageId),
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
        ReplyParameters? replyParams = isFirst ? BuildReplyParameters(replyToMessageId) : null;
        await bot.SendMessage(chatId, chunk,
          entities: chunkEntities.Count > 0 ? chunkEntities : null,
          replyParameters: replyParams,
          cancellationToken: ct);

        offset += length;
        isFirst = false;
      }
    }

    private static ReplyParameters? BuildReplyParameters(int? replyToMessageId)
      => replyToMessageId is { } messageId ? new ReplyParameters { MessageId = messageId } : null;

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
