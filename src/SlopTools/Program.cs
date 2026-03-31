using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SlopTools;

internal class Program
{
  private static async Task Main(string[] args)
    {
      Console.Write("Enter bot token: ");
      string? token = Console.ReadLine()?.Trim();
      if(string.IsNullOrEmpty(token))
      {
        Console.Error.WriteLine("Bot token is required.");
        return;
      }

      TelegramBotClient bot = new(token);

      while(true)
      {
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  1. Get chat ID by group link/username");
        Console.WriteLine("  2. List recent chats from updates");
        Console.WriteLine("  3. Exit");
        Console.Write("> ");

        string? choice = Console.ReadLine()?.Trim();

        switch(choice)
        {
          case "1":
            await GetChatByLink(bot);
            break;
          case "2":
            await ListRecentChats(bot);
            break;
          case "3":
            return;
          default:
            Console.WriteLine("Invalid option.");
            break;
        }
      }
    }

    private static async Task GetChatByLink(TelegramBotClient bot)
    {
      Console.Write("Enter group link or @username: ");
      string? input = Console.ReadLine()?.Trim();
      if(string.IsNullOrEmpty(input))
      {
        Console.Error.WriteLine("Input is required.");
        return;
      }

      string chatIdentifier = ParseChatIdentifier(input);

      try
      {
        ChatFullInfo chat = await bot.GetChat(chatIdentifier);
        PrintChatInfo(chat);
      }
      catch(Exception ex)
      {
        Console.Error.WriteLine($"Error: {ex.Message}");
        Console.Error.WriteLine("Note: this only works for public groups with a username, and the bot must be a member.");
        Console.Error.WriteLine("For private groups, use option 2 (the bot must have received a message in the group).");
      }
    }

    private static async Task ListRecentChats(TelegramBotClient bot)
    {
      try
      {
        Update[] updates = await bot.GetUpdates(offset: -100);

        if(updates.Length == 0)
        {
          Console.WriteLine("No recent updates. Send a message in a group where the bot is a member, then try again.");
          return;
        }

        HashSet<long> seen = new();

        foreach(Update update in updates)
        {
          Chat? chat = update.Message?.Chat;
          if(chat is null || chat.Type == ChatType.Private)
          {
            continue;
          }

          if(!seen.Add(chat.Id))
          {
            continue;
          }

          Console.WriteLine($"  Chat ID: {chat.Id}");
          Console.WriteLine($"  Title:   {chat.Title}");
          Console.WriteLine($"  Type:    {chat.Type}");
          Console.WriteLine();
        }

        if(seen.Count == 0)
        {
          Console.WriteLine("No group chats found in recent updates. Send a message in a group where the bot is a member.");
        }
      }
      catch(Exception ex)
      {
        Console.Error.WriteLine($"Error: {ex.Message}");
      }
    }

    private static string ParseChatIdentifier(string input)
    {
      if(input.StartsWith('@'))
      {
        return input;
      }

      if(input.Contains("t.me/"))
      {
        int index = input.LastIndexOf('/');
        if(index >= 0 && index < input.Length - 1)
        {
          string username = input[(index + 1)..];
          if(!username.StartsWith('+'))
          {
            return $"@{username}";
          }
        }
      }

      return input;
    }

    private static void PrintChatInfo(ChatFullInfo chat)
    {
      Console.WriteLine($"  Chat ID: {chat.Id}");
      Console.WriteLine($"  Title:   {chat.Title}");
      Console.WriteLine($"  Type:    {chat.Type}");
    }
  }
