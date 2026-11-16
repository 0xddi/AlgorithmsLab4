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

    /// <summary>Шаги и команды управления работают на UI-потоке. При нулевой задержке
    /// регулярно отдаём управление интерфейсу, сохраняя каждую операцию журнала.</summary>
    public async Task PlayAsync(Func<bool> advance, CancellationToken ct)
    {
        var frame = Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (_paused) await _resume.Task.WaitAsync(ct);
            ct.ThrowIfCancellationRequested();
            if (!advance()) return;
            if (DelayMs > 0) await Task.Delay(DelayMs, ct);
            else if (frame.ElapsedMilliseconds >= 16)
            {
                await Task.Delay(1, ct);
                frame.Restart();
            }
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
