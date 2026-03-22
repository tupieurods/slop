namespace SlopChat.Configuration
{
    public class BotOptions
    {
        public const string SectionName = "SLOP";

        public string TelegramToken { get; set; } = string.Empty;

        public string OpenRouterKey { get; set; } = string.Empty;

        public long AdminId { get; set; }

        public HashSet<long> AllowedChats { get; set; } = new();

        public string SystemPrompt { get; set; } = "You are a helpful AI assistant.";

        public string DefaultModel { get; set; } = "google/gemini-2.5-flash-preview";

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
