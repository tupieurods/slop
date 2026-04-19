using Telegram.Bot;
using Telegram.Bot.Types;

namespace SlopChat.Services;

public static class TelegramMediaDownloader
{
  public static async Task<string?> DownloadPhotoAsDataUrlAsync(
    ITelegramBotClient bot,
    PhotoSize[] photos,
    CancellationToken ct)
  {
    if(photos.Length == 0)
    {
      return null;
    }

    try
    {
      PhotoSize largest = photos[^1];

      var file = await bot.GetFile(largest.FileId, ct);
      if(file.FilePath is null)
      {
        return null;
      }

      using var stream = new MemoryStream();
      await bot.DownloadFile(file.FilePath, stream, ct);
      byte[] bytes = stream.ToArray();
      string base64 = Convert.ToBase64String(bytes);

      return $"data:image/jpeg;base64,{base64}";
    }
    catch
    {
      return null;
    }
  }
}
