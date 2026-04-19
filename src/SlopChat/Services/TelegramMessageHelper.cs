using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Services
{
  public static class TelegramMessageHelper
  {
    private const int MaxMessageLength = 4096;

    public static async Task SendChunkedAsync(ITelegramBotClient bot, long chatId, string text, int replyToMessageId, CancellationToken ct)
    {
      if(text.Length <= MaxMessageLength)
      {
        await bot.SendMessage(chatId, text, replyParameters: new ReplyParameters { MessageId = replyToMessageId }, cancellationToken: ct);
        return;
      }

      int offset = 0;
      bool isFirst = true;
      while(offset < text.Length)
      {
        int length = Math.Min(MaxMessageLength, text.Length - offset);

        if(offset + length < text.Length)
        {
          int newlineIndex = text.LastIndexOf('\n', offset + length - 1, length);
          if(newlineIndex > offset)
          {
            length = newlineIndex - offset + 1;
          }
        }

        string chunk = text.Substring(offset, length);
        ReplyParameters? replyParams = isFirst ? new ReplyParameters { MessageId = replyToMessageId } : null;
        await bot.SendMessage(chatId, chunk, replyParameters: replyParams, cancellationToken: ct);

        offset += length;
        isFirst = false;
      }
    }
  }
}
