using System.Diagnostics;

namespace SortLab.Services;

/// <summary>
/// Общий проигрыватель шагов: задержка между шагами, пауза/продолжение, отмена.
/// Используется всеми тремя вкладками.
/// </summary>
public sealed class PlaybackEngine
{
    private volatile bool _paused;
    private TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Задержка между шагами, мс (0–2000).</summary>
    public int DelayMs { get; set; }

    public bool IsPaused => _paused;

    public void Pause() => _paused = true;

    public void Resume()
    {
        if (_paused)
        {
            _paused = false;
            _resume.TrySetResult();
            _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Сброс: снять паузу, чтобы отменённый цикл мог выйти.</summary>
    public void Reset()
    {
        _paused = false;
        _resume.TrySetResult();
        _resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Ожидание одного такта: сначала пауза (если включена), затем задержка.</summary>
    public async Task WaitAsync(CancellationToken ct)
    {
        while (_paused)
        {
            ct.ThrowIfCancellationRequested();
            await _resume.Task.WaitAsync(ct);
        }
        ct.ThrowIfCancellationRequested();
        if (DelayMs > 0)
        {
            // Задержка «кусочками», чтобы пауза срабатывала почти мгновенно.
            int left = DelayMs;
            while (left > 0)
            {
                if (_paused) return; // дальше снова заходим в WaitAsync и попадём в ожидание паузы
                int chunk = Math.Min(15, left);
                await Task.Delay(chunk, ct);
                left -= chunk;
            }
        }
        else
        {
            await Task.Yield();
        }
    }
}

/// <summary>Статистика «тихого» прогона алгоритма (без визуализации).</summary>
public sealed record SilentRunStats(int Comparisons, int Swaps, int Assignments, int TotalSteps, double TimeMs);

public static class SilentRunner
{
    /// <summary>Прогнать алгоритм без отображения и собрать статистику (для счётчиков и времени).</summary>
    public static SilentRunStats RunInternal(IEnumerable<Models.SortStep> steps)
    {
        int cmp = 0, swp = 0, asg = 0, total = 0;
        var sw = Stopwatch.StartNew();
        foreach (var s in steps)
        {
            total++;
            switch (s.Op)
            {
                case Models.SortOpType.Compare: cmp++; break;
                case Models.SortOpType.Swap: swp++; break;
                case Models.SortOpType.Assign: asg++; break;
            }
        }
        sw.Stop();
        return new SilentRunStats(cmp, swp, asg, total, sw.Elapsed.TotalMilliseconds);
    }

    /// <summary>Подсчёт операций в потоке шагов без учёта времени.</summary>
    public static (int Cmp, int Swp, int Asg, int Total) Count(IEnumerable<Models.SortStep> steps)
    {
        int cmp = 0, swp = 0, asg = 0, total = 0;
        foreach (var s in steps)
        {
            total++;
            switch (s.Op)
            {
                case Models.SortOpType.Compare: cmp++; break;
                case Models.SortOpType.Swap: swp++; break;
                case Models.SortOpType.Assign: asg++; break;
            }
        }
        return (cmp, swp, asg, total);
    }
}
