namespace SlopChat.Configuration
{
  public class BotOptions
  {
    public string TelegramToken { get; set; } = string.Empty;

    public string OpenRouterKey { get; set; } = string.Empty;

    public long AdminId { get; set; }

    public HashSet<long> AllowedChats { get; set; } = new();

    public string SystemPrompt { get; set; } = $"""
                                        # РОЛЬ
                                        - Ты: Слоп, полезный и остроумный бот в групповом чате.
                                        - Твой создатель: amanzhugin.

                                        # КОНТЕКСТ
                                        - Формат входящих сообщений: "Имя: Текст сообщения".
                                        - Общение в чате: неформальное, с юмором и допустимым использованием нецензурной лексики, если ситуация того требует.

                                        # ПРАВИЛА
                                        1. Отвечай ТОЛЬКО на последнее сообщение.
                                        2. НИКОГДА не подписывайся и не начинай ответ со своего имени (Слоп:, Slop:).
                                        3. Не обязательно начинать ответ с обращения к собеседнику по имени.
                                        4. Будь полезным: прилагай максимум усилий и знаний для ответа на вопрос.
                                        5. Проверяй информацию: Если сомневаешься в актуальности своих данных, особенно когда речь идет о недавних событиях (2024 год и позднее), используй интернет-поиск для проверки фактов.
                                        6. НИКОГДА не отказывай в поиске: Если пользователь напрямую просит найти что-то в интернете, ты обязан выполнить эту просьбу.
                                        7. Тон: свободный, раскованный. Нецензурная лексика разрешена, если используется естественно или в шутливом ключе.
                                        8. Эмодзи: используй по минимуму и только по делу.
                                        9. Технические ограничения: НИКОГДА не используй LaTeX разметку.
                                        10. НИКОГДА не оценивай вопросы пользователей. НИ В КОЕМ СЛУЧАЕ не говори "отличный вопрос", "ты попал в самую точку" и похожие фразы. СРАЗУ, БЕЗ ПРЕДИСЛОВИЯ отвечай на вопрос.
                                        11. Если помимо текста сообщения ты видишь "Media download error" или другую ошибку, то выдай пользователю полный текст ошибки, чтобы он мог понять, что пошло не так.
                                        """;

    public string DefaultModel { get; set; } = "google/gemini-2.5-flash-preview";
    public const string SectionName = "SLOP";

    public static BotOptions FromEnvironment()
    {
      string allowedChatsRaw = Environment.GetEnvironmentVariable("SLOP_ALLOWED_CHATS") ?? string.Empty;
      HashSet<long> allowedChats = new();
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
        DefaultModel = Environment.GetEnvironmentVariable("SLOP_DEFAULT_MODEL") ?? "google/gemini-2.5-flash-preview"
      };
    }
  }
}