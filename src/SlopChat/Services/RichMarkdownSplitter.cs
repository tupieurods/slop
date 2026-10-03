using System.Text;

namespace SlopChat.Services
{
  public static class RichMarkdownSplitter
  {
    private const int MinFenceLength = 3;

    public static List<string> Split(string markdown, int maxLength)
    {
      ArgumentNullException.ThrowIfNull(markdown);
      ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);

      if(markdown.Length <= maxLength)
      {
        return [markdown];
      }

      List<string> chunks = [];
      var current = new StringBuilder();

      foreach(string paragraph in SplitIntoParagraphs(markdown))
      {
        if(current.Length + paragraph.Length <= maxLength)
        {
          current.Append(paragraph);
          continue;
        }

        FlushChunk(current, chunks);

        if(paragraph.Length <= maxLength)
        {
          current.Append(paragraph);
          continue;
        }

        current.Append(SplitOversizedParagraph(paragraph, maxLength, chunks));
      }

      FlushChunk(current, chunks);
      return chunks;
    }

    private static List<string> SplitIntoParagraphs(string markdown)
    {
      List<string> paragraphs = [];
      Fence? fence = null;
      int paragraphStart = 0;
      int lineStart = 0;

      while(lineStart < markdown.Length)
      {
        int newlineIndex = markdown.IndexOf('\n', lineStart);
        int lineEnd = newlineIndex < 0 ? markdown.Length : newlineIndex + 1;
        ReadOnlySpan<char> line = markdown.AsSpan(lineStart, lineEnd - lineStart);
        Fence? nextFence = NextFenceState(fence, line);

        if(fence is null && nextFence is null && line.IsWhiteSpace())
        {
          paragraphs.Add(markdown[paragraphStart..lineEnd]);
          paragraphStart = lineEnd;
        }

        fence = nextFence;
        lineStart = lineEnd;
      }

      if(paragraphStart < markdown.Length)
      {
        paragraphs.Add(markdown[paragraphStart..]);
      }

      return paragraphs;
    }

    // Emits all full pieces into chunks and returns the tail that still fits, so it can be packed with following paragraphs.
    private static string SplitOversizedParagraph(string paragraph, int maxLength, List<string> chunks)
    {
      string reopen = string.Empty;
      int offset = 0;

      while(reopen.Length + paragraph.Length - offset > maxLength)
      {
        (string piece, int consumed, string nextReopen) = CutPiece(paragraph, offset, reopen, maxLength);
        chunks.Add(piece);
        offset += consumed;
        reopen = nextReopen;
      }

      return string.Concat(reopen, paragraph.AsSpan(offset));
    }

    // A piece cut inside a code fence is closed at its end, and the fence is reopened at the start of the next piece.
    private static (string Piece, int Consumed, string NextReopen) CutPiece(string paragraph, int offset, string reopen, int maxLength)
    {
      int available = maxLength - reopen.Length;
      int budget = available;

      while(budget > 0)
      {
        int length = ExtendOverFenceLine(paragraph, offset, FindCutLength(paragraph, offset, budget));
        int cut = offset + length;
        Fence? fence = FenceStateAt(paragraph, cut);
        string close = fence.HasValue ? BuildFenceClose(paragraph, cut, fence.Value) : string.Empty;

        if(length + close.Length <= available)
        {
          string nextReopen = fence.HasValue ? fence.Value.Opener + "\n" : string.Empty;
          return (string.Concat(reopen, paragraph.AsSpan(offset, length), close), length, nextReopen);
        }

        budget = Math.Min(budget - 1, available - close.Length);
      }

      // Degenerate case: maxLength cannot hold the fence opener plus its closing line, so cut without fence handling.
      int rawLength = FindCutLength(paragraph, offset, maxLength);
      return (paragraph.Substring(offset, rawLength), rawLength, string.Empty);
    }

    // A cut in the middle of a fence opener or closer line would destroy the fence, so such a line is kept whole.
    private static int ExtendOverFenceLine(string text, int offset, int length)
    {
      int cut = offset + length;
      if(cut >= text.Length || text[cut - 1] == '\n')
      {
        return length;
      }

      int lineStart = text.LastIndexOf('\n', cut - 1) + 1;
      int newlineIndex = text.IndexOf('\n', cut);
      int lineEnd = newlineIndex < 0 ? text.Length : newlineIndex + 1;
      Fence? before = FenceStateAt(text, lineStart);
      Fence? after = NextFenceState(before, text.AsSpan(lineStart, lineEnd - lineStart));

      return before == after ? length : lineEnd - offset;
    }

    private static int FindCutLength(string text, int offset, int maxLength)
    {
      int remaining = text.Length - offset;
      if(remaining <= maxLength)
      {
        return remaining;
      }

      int newlineIndex = text.LastIndexOf('\n', offset + maxLength - 1, maxLength);
      if(newlineIndex >= offset)
      {
        return newlineIndex - offset + 1;
      }

      bool splitsSurrogatePair = maxLength > 1 && char.IsHighSurrogate(text[offset + maxLength - 1]);
      return splitsSurrogatePair ? maxLength - 1 : maxLength;
    }

    private static Fence? FenceStateAt(string text, int end)
    {
      Fence? fence = null;
      int lineStart = 0;

      while(lineStart < end)
      {
        int newlineIndex = text.IndexOf('\n', lineStart, end - lineStart);
        if(newlineIndex < 0)
        {
          break;
        }

        fence = NextFenceState(fence, text.AsSpan(lineStart, newlineIndex - lineStart));
        lineStart = newlineIndex + 1;
      }

      return fence;
    }

    private static Fence? NextFenceState(Fence? fence, ReadOnlySpan<char> line)
    {
      ReadOnlySpan<char> trimmed = line.Trim();
      if(trimmed.Length < MinFenceLength || (trimmed[0] != '`' && trimmed[0] != '~'))
      {
        return fence;
      }

      char marker = trimmed[0];
      int runLength = trimmed.Length - trimmed.TrimStart(marker).Length;
      if(runLength < MinFenceLength)
      {
        return fence;
      }

      if(fence is { } open)
      {
        bool closes = marker == open.Marker && runLength >= open.Length && runLength == trimmed.Length;
        return closes ? null : fence;
      }

      bool invalidBacktickInfo = marker == '`' && trimmed[runLength..].Contains('`');
      return invalidBacktickInfo ? null : new Fence(marker, runLength, trimmed.ToString());
    }

    private static string BuildFenceClose(string text, int cut, Fence fence)
    {
      string marker = new(fence.Marker, fence.Length);
      return cut > 0 && text[cut - 1] == '\n' ? marker : "\n" + marker;
    }

    private static void FlushChunk(StringBuilder current, List<string> chunks)
    {
      if(current.Length == 0)
      {
        return;
      }

      chunks.Add(current.ToString());
      current.Clear();
    }

    private readonly record struct Fence(char Marker, int Length, string Opener);
  }
}
