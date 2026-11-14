using SortLab.Models;

namespace SortLab.Algorithms.External;

/// <summary>Внешняя сортировка (метод слияния с лентами/файлами): поток шагов для визуализации.</summary>
public interface IExternalSorter
{
    string Name { get; }
    string Description { get; }

    /// <summary>Число лент, используемых сортировщиком (для многопутевого слияния — k).</summary>
    bool NeedsK { get; }

    IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp);
}
