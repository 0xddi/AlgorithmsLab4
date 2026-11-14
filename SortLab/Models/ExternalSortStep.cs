namespace SortLab.Models;

/// <summary>Чип (маленький блок) с записью для визуализации лент/серий.</summary>
public sealed class ChipVm
{
    public string Text { get; init; } = "";
    public int RecordId { get; init; }
    public bool IsCompared { get; init; }
    public bool IsMoved { get; init; }
}

/// <summary>Один отрезок (серия) на ленте.</summary>
public sealed record SeriesBlock(IReadOnlyList<ChipVm> Chips);

/// <summary>Состояние одной ленты (файла) на текущем шаге.</summary>
public sealed record TapeState(string Name, IReadOnlyList<SeriesBlock> Series);

/// <summary>Один шаг внешней сортировки: полные состояния лент + пояснение.</summary>
public sealed record ExternalSortStep(
    SortOpType Op,
    string Phase,
    IReadOnlyList<TapeState> Tapes,
    string Explanation,
    string LogIndices,
    string LogValues);
