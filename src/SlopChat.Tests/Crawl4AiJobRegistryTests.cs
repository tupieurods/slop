using Microsoft.Extensions.Logging.Abstractions;
using SlopMcp.Services;

namespace SlopChat.Tests
{
  public class Crawl4AiJobRegistryTests
  {
    private static Crawl4AiJobRegistry CreateRegistry()
      => new Crawl4AiJobRegistry(NullLogger<Crawl4AiJobRegistry>.Instance, TimeProvider.System);

    private static Crawl4AiJobRegistry CreateRegistryWithTtl(TimeSpan stashTtl)
      => new Crawl4AiJobRegistry(NullLogger<Crawl4AiJobRegistry>.Instance, TimeProvider.System, stashTtl);

    [Fact]
    public async Task RegisterAndAwait_ThenComplete_ResolvesWithMarkdown()
    {
      var registry = CreateRegistry();
      var task = registry.RegisterAndAwaitAsync("task1", TimeSpan.FromSeconds(30), CancellationToken.None);

      registry.Complete("task1", "# Hello");

      var result = await task;
      Assert.Equal("# Hello", result.Markdown);
    }

    [Fact]
    public async Task RegisterAndAwait_Timeout_FaultsWithMessage()
    {
      var registry = CreateRegistry();
      var task = registry.RegisterAndAwaitAsync("task2", TimeSpan.FromMilliseconds(50), CancellationToken.None);

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("task2", ex.Message);
      Assert.Contains("did not arrive within", ex.Message);
    }

    [Fact]
    public async Task RegisterAndAwait_Timeout_JobCountIsZeroAfterTimeout()
    {
      var registry = CreateRegistry();
      var task = registry.RegisterAndAwaitAsync("timeout-dispose", TimeSpan.FromMilliseconds(50), CancellationToken.None);

      await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Equal(0, registry.ActiveJobCount);
    }

    [Fact]
    public async Task RegisterAndAwait_Fail_FaultsWithReason()
    {
      var registry = CreateRegistry();
      var task = registry.RegisterAndAwaitAsync("task3", TimeSpan.FromSeconds(30), CancellationToken.None);

      registry.Fail("task3", "something went wrong");

      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("something went wrong", ex.Message);
    }

    [Fact]
    public void Complete_UnknownTaskId_IsNoOp()
    {
      var registry = CreateRegistry();
      var ex = Record.Exception(() => registry.Complete("unknown-id", "markdown"));
      Assert.Null(ex);
    }

    [Fact]
    public void Fail_UnknownTaskId_IsNoOp()
    {
      var registry = CreateRegistry();
      var ex = Record.Exception(() => registry.Fail("unknown-id", "error"));
      Assert.Null(ex);
    }

    [Fact]
    public async Task RegisterAndAwait_ExternalCancellation_CancelsTask()
    {
      var registry = CreateRegistry();
      using var cts = new CancellationTokenSource();

      var task = registry.RegisterAndAwaitAsync("task4", TimeSpan.FromSeconds(30), cts.Token);
      cts.Cancel();

      await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public void Register_DuplicateTaskId_Throws()
    {
      var registry = CreateRegistry();
      registry.RegisterAndAwaitAsync("dup", TimeSpan.FromSeconds(30), CancellationToken.None);

      try
      {
        registry.RegisterAndAwaitAsync("dup", TimeSpan.FromSeconds(30), CancellationToken.None);
        Assert.Fail("Expected InvalidOperationException was not thrown");
      }
      catch(InvalidOperationException ex)
      {
        Assert.Contains("dup", ex.Message);
      }
    }

    [Fact]
    public async Task Complete_ArrivingBeforeRegister_ResolvesImmediatelyOnRegister()
    {
      var registry = CreateRegistry();

      registry.Complete("early-task", "# Stashed markdown");

      var result = await registry.RegisterAndAwaitAsync("early-task", TimeSpan.FromSeconds(5), CancellationToken.None);

      Assert.Equal("# Stashed markdown", result.Markdown);
    }

    [Fact]
    public async Task Fail_ArrivingBeforeRegister_ResolvesWithErrorOnRegister()
    {
      var registry = CreateRegistry();

      registry.Fail("early-fail", "upstream error");

      var task = registry.RegisterAndAwaitAsync("early-fail", TimeSpan.FromSeconds(5), CancellationToken.None);
      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("upstream error", ex.Message);
    }

    [Fact]
    public async Task Complete_ArrivingBeforeRegister_ExpiresIfNeverRegistered()
    {
      var registry = CreateRegistryWithTtl(TimeSpan.FromMilliseconds(10));

      registry.Complete("orphan-task", "# Will expire");

      await Task.Delay(50);

      _ = registry.RegisterAndAwaitAsync("sweep-trigger", TimeSpan.FromMilliseconds(1), CancellationToken.None);

      Assert.Equal(0, registry.StashCount);
    }

    [Fact]
    public async Task RegisterAndAwait_CompleteViaTestHook_ResolvesWithoutException()
    {
      var registry = CreateRegistry();
      registry._testHookAfterJobsAdd = () => registry.Complete("hook-task", "# Hook markdown");

      var result = await registry.RegisterAndAwaitAsync("hook-task", TimeSpan.FromSeconds(5), CancellationToken.None);

      Assert.Equal("# Hook markdown", result.Markdown);
    }

    [Fact]
    public async Task RegisterAndAwait_FailViaTestHook_FaultsWithReason()
    {
      var registry = CreateRegistry();
      registry._testHookAfterJobsAdd = () => registry.Fail("hook-fail", "late failure");

      var task = registry.RegisterAndAwaitAsync("hook-fail", TimeSpan.FromSeconds(5), CancellationToken.None);
      var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);
      Assert.Contains("late failure", ex.Message);
    }

    [Fact]
    public async Task RegisterAndAwait_ImmediateComplete_ReturnsCompletedValueNoException()
    {
      var registry = CreateRegistry();

      var task = registry.RegisterAndAwaitAsync("imm-task", TimeSpan.FromSeconds(30), CancellationToken.None);
      registry.Complete("imm-task", "# Immediate");

      var result = await task;
      Assert.Equal("# Immediate", result.Markdown);
    }
  }
}
