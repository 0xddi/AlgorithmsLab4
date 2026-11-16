using System.Diagnostics;
using Avalonia.Threading;
using SortLab.Algorithms.Strings;
using SortLab.Models;

namespace SortLab.Services;

/// <summary>Сервис экспериментов: генерация текстов, замер времени сортировки, сбор статистики.</summary>
public sealed class BenchmarkService
{
    /// <summary>
    /// Прогнать все алгоритмы на текстах заданных длин. Выполняется в фоне; прогресс
    /// отдаётся через callback, который обязан маршалить обновления на UI-поток.
    /// </summary>
    public async Task<List<BenchmarkResult>> RunAsync(
        int[] lengths, IStringSortAlgorithm[] algorithms, int runs,
        Action<int, string> progress, CancellationToken ct)
    {
        return await Task.Run(async () =>
        {
            var results = new List<BenchmarkResult>();
            int total = lengths.Length * algorithms.Length;
            int done = 0;

            foreach (var len in lengths)
            {
                var words = TextProcessor.SplitWords(TextGenerator.GenerateText(len), ignoreCase: true);
                foreach (var algo in algorithms)
                {
                    ct.ThrowIfCancellationRequested();
                    progress(done, $"Длина {len}: {algo.Name}...");
                    var times = new List<double>();
                    long cmp = 0, moves = 0;
                    for (int run = 0; run < runs; run++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var sw = Stopwatch.StartNew();
                        cmp = 0;
                        moves = 0;
                        // snapshots=false: без снимков массива на каждый шаг — быстро и без лишней памяти.
                        foreach (var step in algo.GetSteps(words, snapshots: false))
                        {
                            switch (step.Op)
                            {
                                case SortOpType.Compare: cmp++; break;
                                case SortOpType.Swap:
                                case SortOpType.Assign: moves++; break;
                            }
                        }
                        sw.Stop();
                        times.Add(sw.Elapsed.TotalMilliseconds);
                    }
                    results.Add(new BenchmarkResult
                    {
                        Length = len,
                        Algorithm = algo.Name,
                        AvgMs = times.Average(),
                        MinMs = times.Min(),
                        Comparisons = cmp,
                        Moves = moves,
                    });
                    done++;
                    progress(done * 100 / Math.Max(1, total), $"Готово: {algo.Name}, длина {len}");
                }
            }
            return results;
        }, ct);
    }
}
