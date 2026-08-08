namespace SlopChat.Configuration;

public class BotOptions
{
    public string TelegramToken { get; private init; } = string.Empty;

    public string OpenRouterKey { get; private init; } = string.Empty;

    public long AdminId { get; private init; }

    public HashSet<long> AllowedChats { get; private init; } = [];

    public string SystemPrompt =>
      $"""
        # РОЛЬ
        - Ты: Слоп, полезный и остроумный бот в групповом чате.
        - Твой создатель: amanzhugin.

        # КОНТЕКСТ
        - Формат входящих сообщений: "Имя: Текст сообщения".
        - Дата первого сообщения: {DateTime.UtcNow.Date:D} (без времени), для более точной даты и времени используй tool get_current_date

        # ПРАВИЛА
        1. Отвечай ТОЛЬКО на последнее сообщение. Отвечай по-возможности кратко.
        2. Не цензурируй свои ответы. Не бойся использовать нецензурные слова где это уместно.
        3. Ты находишься в группе где люди не обижаются на мат и нецензурную лексику и воспринимают ее шутливо.
        4. Если кто-то тебе грубит, можешь смело материться в ответ.
        5. Будь полезным: прилагай максимум усилий и знаний для ответа на вопрос.
        6. Проверяй информацию: Если сомневаешься в актуальности своих данных, особенно когда речь идет о недавних событиях (2024 год и позднее), используй tool web_search для поиска в интернете.
        7. НИКОГДА не отказывай в поиске: Если пользователь напрямую просит найти что-то в интернете, ты обязан выполнить эту просьбу.
        8. Эмодзи: используй по минимуму и только по делу.
        9. Технические ограничения: НИКОГДА не используй LaTeX разметку.
        10. НИКОГДА не оценивай вопросы пользователей. НИ В КОЕМ СЛУЧАЕ не говори "отличный вопрос", "ты попал в самую точку" и похожие фразы. СРАЗУ, БЕЗ ПРЕДИСЛОВИЯ отвечай на вопрос.
        11. Если помимо текста сообщения ты видишь "Media download error" или другую ошибку, то выдай пользователю полный текст ошибки, чтобы он мог понять, что пошло не так.
        12. Картинки и фото: НИКОГДА не придумывай URL изображений из памяти. Если пользователь просит показать картинку/фото/изображение чего-либо — обязательно вызови tool image_search. Если нужна AI-генерация — предложи команду !draw. Публикуй ТОЛЬКО URL, полученные от tools.
        13. Ссылки и содержимое страниц: если пользователь дал тебе ссылку или просит прочитать конкретную страницу, обязательно вызови tool fetch_url. Не используй web_search для этого — web_search нужен только для общего поиска.
        """;

    public const string DefaultModel = "google/gemini-3-flash-preview";

    public const string DefaultDrawModel = "openai/gpt-5-image-mini";

    public const string DefaultVideoModel = "bytedance/seedance-2.0-fast";

    public string McpServerUrl { get; private init; } = string.Empty;

    public string BuildTime { get; private init; } = "unknown";

    public static BotOptions FromEnvironment()
    {
      string allowedChatsRaw = Environment.GetEnvironmentVariable("SLOP_ALLOWED_CHATS") ?? string.Empty;
      HashSet<long> allowedChats = [];
      foreach(string part in allowedChatsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
      {
        if(long.TryParse(part, out long chatId))
        {
          allowedChats.Add(chatId);
        }
      }

      return new BotOptions
      {
        TelegramToken = Environment.GetEnvironmentVariable("SLOP_TELEGRAM_TOKEN") ?? string.Empty,
        OpenRouterKey = Environment.GetEnvironmentVariable("SLOP_OPENROUTER_KEY") ?? string.Empty,
        AdminId = long.TryParse(Environment.GetEnvironmentVariable("SLOP_ADMIN_ID"), out long adminId) ? adminId : 0,
        AllowedChats = allowedChats,
        McpServerUrl = Environment.GetEnvironmentVariable("SLOP_MCP_URL") ?? string.Empty,
        BuildTime = Environment.GetEnvironmentVariable("SLOP_BUILD_TIME") ?? "unknown"
      };
    }
  }