using System.Collections.Concurrent;
using SlopMcp.Models;

namespace SlopMcp.Services {

  public class Crawl4AiJobRegistry
  {
    private readonly ILogger<Crawl4AiJobRegistry> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _stashTtl;
    private readonly ConcurrentDictionary<string, PendingJob> _jobs = new();
    private readonly ConcurrentDictionary<string, StashedResult> _stash = new();

    internal Action? _testHookAfterJobsAdd;

    public Crawl4AiJobRegistry(ILogger<Crawl4AiJobRegistry> logger, TimeProvider timeProvider)
      : this(logger, timeProvider, TimeSpan.FromMinutes(5))
    {
    }

    internal Crawl4AiJobRegistry(
      ILogger<Crawl4AiJobRegistry> logger,
      TimeProvider timeProvider,
      TimeSpan stashTtl
    )
    {
      _logger = logger;
      _timeProvider = timeProvider;
      _stashTtl = stashTtl;
    }

    internal int ActiveJobCount => _jobs.Count;
    internal int StashCount => _stash.Count;

    public Task<Crawl4AiResult> RegisterAndAwaitAsync(
      string taskId,
      TimeSpan timeout,
      CancellationToken ct
    )
    {
      SweepExpiredStash();

      if(_stash.TryRemove(taskId, out var stashed))
      {
        if(stashed.IsSuccess)
        {
          _logger.LogInformation(
            "Crawl4AI job resolved from stash (callback arrived before register): task_id={TaskId}",
            taskId
          );
          return Task.FromResult(new Crawl4AiResult { Markdown = stashed.Markdown! });
        }
        else
        {
          _logger.LogWarning(
            "Crawl4AI job failed from stash (callback arrived before register): task_id={TaskId}, reason={Reason}",
            taskId, stashed.Reason
          );
          return Task.FromException<Crawl4AiResult>(new InvalidOperationException(stashed.Reason));
        }
      }

      var tcs = new TaskCompletionSource<Crawl4AiResult>(TaskCreationOptions.RunContinuationsAsynchronously);
      long startTs = _timeProvider.GetTimestamp();

      var timeoutCts = new CancellationTokenSource(timeout);
      var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);
      CancellationToken linked = linkedCts.Token;

      CancellationTokenRegistration reg = linked.Register(() =>
      {
        if(!_jobs.TryRemove(taskId, out var removed))
        {
          return;
        }

        long elapsedMs = (long)_timeProvider.GetElapsedTime(removed.StartTs).TotalMilliseconds;

        if(ct.IsCancellationRequested)
        {
          _logger.LogWarning(
            "Crawl4AI job cancelled by caller: task_id={TaskId}, elapsed={ElapsedMs}ms",
            taskId, elapsedMs
          );
          removed.Tcs.TrySetCanceled(ct);
        }
        else
        {
          _logger.LogWarning(
            "Crawl4AI callback did not arrive within {TimeoutSeconds}s: task_id={TaskId}",
            (int)timeout.TotalSeconds, taskId
          );
          removed.Tcs.TrySetException(new InvalidOperationException(
            $"Crawl4AI callback did not arrive within {(int)timeout.TotalSeconds}s for task_id {taskId}"
          ));
        }

        removed.Dispose();
      });

      var job = new PendingJob(tcs, timeoutCts, linkedCts, reg, startTs);

      if(!_jobs.TryAdd(taskId, job))
      {
        reg.Dispose();
        job.Dispose();
        throw new InvalidOperationException($"Duplicate task_id registered: {taskId}");
      }

      // If the token was already cancelled before TryAdd, the Register callback fired
      // synchronously before the job existed in _jobs, so TryRemove inside it was a no-op.
      // Detect that here and drive the cancellation path ourselves.
      if(linked.IsCancellationRequested && _jobs.TryRemove(taskId, out var preempted))
      {
        preempted.Dispose();

        if(ct.IsCancellationRequested)
        {
          tcs.TrySetCanceled(ct);
        }
        else
        {
          tcs.TrySetException(new InvalidOperationException(
            $"Crawl4AI callback did not arrive within {(int)timeout.TotalSeconds}s for task_id {taskId}"
          ));
        }

        return tcs.Task;
      }

      _testHookAfterJobsAdd?.Invoke();

      if(_stash.TryRemove(taskId, out var late))
      {
        _jobs.TryRemove(taskId, out _);
        job.Dispose();

        if(late.IsSuccess)
        {
          _logger.LogInformation(
            "Crawl4AI job resolved from late stash (callback arrived between stash-check and jobs-add): task_id={TaskId}",
            taskId
          );
          tcs.TrySetResult(new Crawl4AiResult { Markdown = late.Markdown! });
        }
        else
        {
          _logger.LogWarning(
            "Crawl4AI job failed from late stash: task_id={TaskId}, reason={Reason}",
            taskId, late.Reason
          );
          tcs.TrySetException(new InvalidOperationException(late.Reason));
        }

        return tcs.Task;
      }

      _logger.LogInformation(
        "Crawl4AI job registered: task_id={TaskId}, timeout={TimeoutSeconds}s",
        taskId, (int)timeout.TotalSeconds
      );

      return tcs.Task;
    }

    public void Complete(string taskId, string markdown)
    {
      if(!_jobs.TryRemove(taskId, out var job))
      {
        _logger.LogInformation(
          "Crawl4AI callback (Complete) arrived before RegisterAndAwaitAsync: stashing task_id={TaskId}",
          taskId
        );
        _stash[taskId] = new StashedResult(markdown, _timeProvider.GetTimestamp());
        return;
      }

      long elapsedMs = (long)_timeProvider.GetElapsedTime(job.StartTs).TotalMilliseconds;
      _logger.LogInformation(
        "Crawl4AI job completed: task_id={TaskId}, elapsed={ElapsedMs}ms, markdownLength={Length}",
        taskId, elapsedMs, markdown.Length
      );

      job.Tcs.TrySetResult(new Crawl4AiResult { Markdown = markdown });
      job.Dispose();
    }

    public void Fail(string taskId, string reason)
    {
      if(!_jobs.TryRemove(taskId, out var job))
      {
        _logger.LogInformation(
          "Crawl4AI callback (Fail) arrived before RegisterAndAwaitAsync: stashing task_id={TaskId}, reason={Reason}",
          taskId, reason
        );
        _stash[taskId] = new StashedResult(reason, _timeProvider.GetTimestamp(), isSuccess: false);
        return;
      }

      long elapsedMs = (long)_timeProvider.GetElapsedTime(job.StartTs).TotalMilliseconds;
      _logger.LogWarning(
        "Crawl4AI job failed: task_id={TaskId}, reason={Reason}, elapsed={ElapsedMs}ms",
        taskId, reason, elapsedMs
      );

      job.Tcs.TrySetException(new InvalidOperationException(reason));
      job.Dispose();
    }

    private void SweepExpiredStash()
    {
      foreach(var kv in _stash)
      {
        if(_timeProvider.GetElapsedTime(kv.Value.StashedAtTs) > _stashTtl)
        {
          if(_stash.TryRemove(kv.Key, out _))
          {
            _logger.LogWarning(
              "Crawl4AI stashed callback expired without matching RegisterAndAwaitAsync: task_id={TaskId}",
              kv.Key
            );
          }
        }
      }
    }

    private sealed class StashedResult
    {
      public string? Markdown { get; }
      public string? Reason { get; }
      public bool IsSuccess { get; }
      public long StashedAtTs { get; }

      public StashedResult(string value, long stashedAtTs, bool isSuccess = true)
      {
        IsSuccess = isSuccess;
        StashedAtTs = stashedAtTs;

        if(isSuccess)
        {
          Markdown = value;
        }
        else
        {
          Reason = value;
        }
      }
    }

    private sealed class PendingJob : IDisposable
    {
      public TaskCompletionSource<Crawl4AiResult> Tcs { get; }
      public long StartTs { get; }

      private readonly CancellationTokenSource _timeoutCts;
      private readonly CancellationTokenSource _linkedCts;
      private readonly CancellationTokenRegistration _reg;

      public PendingJob(
        TaskCompletionSource<Crawl4AiResult> tcs,
        CancellationTokenSource timeoutCts,
        CancellationTokenSource linkedCts,
        CancellationTokenRegistration reg,
        long startTs
      )
      {
        Tcs = tcs;
        _timeoutCts = timeoutCts;
        _linkedCts = linkedCts;
        _reg = reg;
        StartTs = startTs;
      }

      public void Dispose()
      {
        _reg.Dispose();
        _linkedCts.Dispose();
        _timeoutCts.Dispose();
      }
    }
  }

}
