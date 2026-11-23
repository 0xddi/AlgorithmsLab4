using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Внутренний алгоритм сортировки: возвращает поток шагов для визуализации.</summary>
public interface ISortAlgorithm
{
    string Name { get; }
    string Description { get; }
    string Reference { get; }

    /// <summary>
    /// Ленивая последовательность шагов. Алгоритм не зависит от UI.
    /// При snapshots=false шаги не содержат копий массива: дешёвый режим для замеров времени.
    /// </summary>
    IEnumerable<SortStep> GetSteps(int[] input, bool snapshots);

    /// <summary>Обычный режим визуализации: каждый шаг содержит снимок массива.</summary>
    IEnumerable<SortStep> GetSteps(int[] input) => GetSteps(input, true);
}
