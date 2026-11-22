using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Внутренний алгоритм сортировки: возвращает поток шагов для визуализации.</summary>
public interface ISortAlgorithm
{
    string Name { get; }
    string Description { get; }
    string Reference { get; }

    /// <summary>Ленивая последовательность шагов. Алгоритм не зависит от UI.</summary>
    IEnumerable<SortStep> GetSteps(int[] input);
}
