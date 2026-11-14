namespace SortLab.Models;

/// <summary>Один шаг строковой сортировки (задание 3).</summary>
public sealed record StringSortStep(
    SortOpType Op,
    string[] Values,
    StepMark[] Marks,
    int IndexA,
    int IndexB,
    string Explanation);

/// <summary>Чип со словом для визуализации.</summary>
public sealed class WordChipVm
{
    public string Text { get; init; } = "";
    public StepMark Mark { get; init; }
}

/// <summary>Слово и его частота.</summary>
public sealed class WordFrequency
{
    public string Word { get; init; } = "";
    public int Count { get; init; }
}

/// <summary>Результат одного эксперимента бенчмарка.</summary>
public sealed class BenchmarkResult
{
    public int Length { get; init; }
    public string Algorithm { get; init; } = "";
    public double AvgMs { get; init; }
    public double MinMs { get; init; }
    public long Comparisons { get; init; }
    public long Moves { get; init; }
}

/// <summary>Серия для диаграмм: имя + значения по категориям.</summary>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values);
