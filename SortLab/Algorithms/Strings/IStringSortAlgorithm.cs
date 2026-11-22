using SortLab.Models;

namespace SortLab.Algorithms.Strings;

/// <summary>Алгоритм сортировки строк (слов): поток шагов для визуализации.</summary>
public interface IStringSortAlgorithm
{
    string Name { get; }
    string Description { get; }
    string Reference { get; }

    /// <summary>Шаги с полными снимками (для визуализации).</summary>
    IEnumerable<StringSortStep> GetSteps(string[] words) => GetSteps(words, snapshots: true);

    /// <summary>
    /// Шаги; при snapshots=false снимки не копируются (дёшево — для замеров времени).
    /// </summary>
    IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots);
}

/// <summary>Контекст строковой сортировки: массив слов, подсветка, шаги.</summary>
public sealed class StringCtx
{
    public readonly string[] A;
    public readonly int N;
    private readonly StepMark[] _m;

    /// <summary>Если false — снимки не копируются (дешёвый режим для замеров времени).</summary>
    public bool Snapshot = true;

    public StringCtx(string[] input)
    {
        A = (string[])input.Clone();
        N = A.Length;
        _m = new StepMark[N];
    }

    public StringSortStep S(SortOpType op, int ia, int ib, string explanation) =>
        Snapshot
            ? new(op, (string[])A.Clone(), (StepMark[])_m.Clone(), ia, ib, explanation)
            : new(op, A, _m, ia, ib, explanation);

    public void MarkAll(StepMark m)
    {
        for (int i = 0; i < N; i++) _m[i] = m;
    }

    public void Mark(int i, StepMark m)
    {
        if (i >= 0 && i < N) _m[i] = m;
    }

    public void MarkRange(int from, int to, StepMark m)
    {
        for (int i = Math.Max(0, from); i <= Math.Min(N - 1, to); i++) _m[i] = m;
    }

    public void Swap(int i, int j) => (A[i], A[j]) = (A[j], A[i]);
}
