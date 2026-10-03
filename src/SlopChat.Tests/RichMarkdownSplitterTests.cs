using SlopChat.Services;

namespace SlopChat.Tests
{
  public class RichMarkdownSplitterTests
  {
    [Fact]
    public void ShortInput_ReturnsSingleChunk()
    {
      const string markdown = "# Title\n\nSome **bold** text.";

      var chunks = RichMarkdownSplitter.Split(markdown, 100);

      Assert.Equal([markdown], chunks);
    }

    [Fact]
    public void LongInput_SplitsOnBlankLine()
    {
      string first = new('a', 30);
      string second = new('b', 30);
      string markdown = $"{first}\n\n{second}";

      var chunks = RichMarkdownSplitter.Split(markdown, 40);

      Assert.Equal([$"{first}\n\n", second], chunks);
    }

    [Fact]
    public void BlankLinesInsideCodeFence_AreNotSplitPoints()
    {
      string intro = new('i', 20);
      string fence = "```csharp\nvar a = 1;\n\nvar b = 2;\n\nvar c = 3;\n```\n";
      string markdown = $"{intro}\n\n{fence}";

      var chunks = RichMarkdownSplitter.Split(markdown, fence.Length + 5);

      Assert.Equal([$"{intro}\n\n", fence], chunks);
    }

    [Fact]
    public void OversizedParagraph_SplitsOnNewline()
    {
      string line1 = new('x', 30);
      string line2 = new('y', 30);
      string markdown = $"{line1}\n{line2}";

      var chunks = RichMarkdownSplitter.Split(markdown, 40);

      Assert.Equal([$"{line1}\n", line2], chunks);
    }

    [Fact]
    public void OversizedLineWithoutNewline_IsHardSplit()
    {
      string markdown = new('z', 25);

      var chunks = RichMarkdownSplitter.Split(markdown, 10);

      Assert.Equal([new string('z', 10), new string('z', 10), new string('z', 5)], chunks);
    }

    [Fact]
    public void Null_Throws()
    {
      Assert.Throws<ArgumentNullException>(() => RichMarkdownSplitter.Split(null!, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveMaxLength_Throws(int maxLength)
    {
      Assert.Throws<ArgumentOutOfRangeException>(() => RichMarkdownSplitter.Split("text", maxLength));
    }

    [Fact]
    public void EmptyString_ReturnsSingleEmptyChunk()
    {
      var chunks = RichMarkdownSplitter.Split("", 10);

      Assert.Equal([""], chunks);
    }

    [Fact]
    public void HardSplit_DoesNotBreakSurrogatePair()
    {
      string markdown = new string('a', 9) + "\U0001F600" + "bbb";

      var chunks = RichMarkdownSplitter.Split(markdown, 10);

      Assert.Equal([new string('a', 9), "\U0001F600bbb"], chunks);
    }

    [Fact]
    public void TildeFence_BlankLinesAndBackticksInside_AreNotSplitPoints()
    {
      string intro = new('i', 20);
      string fence = "~~~text\nfirst\n\n```\n\nsecond\n~~~\n";
      string markdown = $"{intro}\n\n{fence}";

      var chunks = RichMarkdownSplitter.Split(markdown, fence.Length + 5);

      Assert.Equal([$"{intro}\n\n", fence], chunks);
    }

    [Fact]
    public void OversizedFencedBlock_EachChunkHasBalancedFences()
    {
      string code = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"var line{i:D2} = {i};"));
      string markdown = $"```csharp\n{code}\n```";
      const int maxLength = 120;

      var chunks = RichMarkdownSplitter.Split(markdown, maxLength);

      Assert.True(chunks.Count > 1);
      Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, maxLength));
      Assert.All(chunks, chunk => Assert.StartsWith("```csharp\n", chunk));
      Assert.All(chunks, chunk => Assert.EndsWith("```", chunk));
      Assert.All(chunks, chunk => Assert.Equal(0, CountFenceLines(chunk) % 2));
      string restoredCode = string.Concat(chunks.Select(chunk => chunk["```csharp\n".Length..^"```".Length]));
      Assert.Equal($"{code}\n", restoredCode);
    }

    [Theory]
    [InlineData("a\n", 20, 20)]
    [InlineData("a\n", 40, 9)]
    [InlineData("ab\n", 30, 12)]
    [InlineData("ab\n", 50, 10)]
    public void OversizedFenceOfShortLines_KeepsOpenerAndBalancedFences(string line, int lineCount, int maxLength)
    {
      string code = string.Concat(Enumerable.Repeat(line, lineCount));
      string markdown = $"```\n{code}```";

      var chunks = RichMarkdownSplitter.Split(markdown, maxLength);

      Assert.True(chunks.Count > 1);
      Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, maxLength));
      Assert.All(chunks, chunk => Assert.StartsWith("```\n", chunk));
      Assert.All(chunks, chunk => Assert.EndsWith("```", chunk));
      Assert.All(chunks, chunk => Assert.Equal(0, CountFenceLines(chunk) % 2));
      string restoredCode = string.Concat(chunks.Select(chunk => chunk["```\n".Length..^"```".Length]));
      Assert.Equal(code, restoredCode);
    }

    [Fact]
    public void Chunks_RespectMaxLengthAndPreserveContent()
    {
      string[] paragraphs =
      [
        "# Heading",
        "| a | b |\n|---|---|\n| 1 | 2 |",
        "```\ncode line\n\nmore code\n```",
        new string('p', 70),
        "- item 1\n- item 2\n- item 3",
        string.Join('\n', Enumerable.Repeat(new string('q', 15), 10))
      ];
      string markdown = string.Join("\n\n", paragraphs);
      const int maxLength = 50;

      var chunks = RichMarkdownSplitter.Split(markdown, maxLength);

      Assert.True(chunks.Count > 1);
      Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, maxLength));
      Assert.Equal(markdown, string.Concat(chunks));
    }

    private static int CountFenceLines(string chunk) => chunk.Split('\n').Count(line => line.TrimStart().StartsWith("```"));
  }
}
