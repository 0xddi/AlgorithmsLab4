using System.Diagnostics;
using SortLab.Algorithms.Strings;
using SortLab.Models;

namespace SortLab.Services;

public enum BenchmarkInputOrder { Random, Ascending, Descending }

/// <summary>Замеры времени и операций строковых сортировок.</summary>
public sealed class BenchmarkService
{
    public Task<List<BenchmarkResult>> RunAsync(
        int[] lengths, IStringSortAlgorithm[] algorithms, int runs, BenchmarkInputOrder order, int seed,
        Action<int, string> progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var results = new List<BenchmarkResult>();
            var random = new Random(seed);
            int total = lengths.Length * algorithms.Length * runs;
            int done = 0;
            int reportedPercent = -1;

            foreach (int length in lengths)
            {
                var samples = algorithms.Select(_ => new List<(double Ms, long Comparisons, long Moves)>(runs)).ToArray();
                for (int run = 0; run < runs; run++)
                {
                    ct.ThrowIfCancellationRequested();
                    string[] words = TextProcessor.SplitWords(TextGenerator.GenerateText(length, random), ignoreCase: true);
                    if (order != BenchmarkInputOrder.Random)
                        Array.Sort(words, StringComparer.Ordinal);
                    if (order == BenchmarkInputOrder.Descending)
                        Array.Reverse(words);

                    for (int i = 0; i < algorithms.Length; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var algorithm = algorithms[i];
                        int percentage = done * 100 / total;
                        if (percentage != reportedPercent)
                        {
                            progress(percentage, $"{length} слов · {algorithm.Name} · прогон {run + 1}/{runs}");
                            reportedPercent = percentage;
                        }

                        long comparisons = 0, moves = 0;
                        int steps = 0;
                        var sw = Stopwatch.StartNew();
                        foreach (var step in algorithm.GetSteps(words, snapshots: false))
                        {
                            if (step.Op == SortOpType.Compare) comparisons++;
                            if (step.Op is SortOpType.Swap or SortOpType.Assign) moves++;
                            if (++steps % 256 == 0) ct.ThrowIfCancellationRequested();
                        }
                        sw.Stop();
                        samples[i].Add((sw.Elapsed.TotalMilliseconds, comparisons, moves));
                        done++;
                    }
                }

                for (int i = 0; i < algorithms.Length; i++)
                {
                    var runsForAlgorithm = samples[i];
                    results.Add(new BenchmarkResult
                    {
                        Length = length,
                        Algorithm = algorithms[i].Name,
                        AvgMs = runsForAlgorithm.Average(s => s.Ms),
                        MinMs = runsForAlgorithm.Min(s => s.Ms),
                        Comparisons = runsForAlgorithm.Average(s => s.Comparisons),
                        Moves = runsForAlgorithm.Average(s => s.Moves),
                    });
                }
            }
            ct.ThrowIfCancellationRequested();
            return results;
        }, ct);
    }
}
