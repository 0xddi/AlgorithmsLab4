using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Algorithms.Internal;
using SortLab.Controls;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>
/// Замеры времени алгоритмов внутренней сортировки (раздел «Массивы»).
/// Объёмы задаются диапазоном «от / до / шаг», чтобы можно было быстро
/// получить густую сетку значений без перечисления каждого объёма.
/// </summary>
public partial class ArrayBenchmarkViewModel : ObservableObject
{
    private const int MinLength = 10;
    private const int MaxLength = 20000;
    private const int MaxVolumes = 100;

    private readonly IReadOnlyList<ISortAlgorithm> _algorithms;
    private readonly BenchmarkService _benchmark;
    private CancellationTokenSource? _cancellation;

    public SettingsViewModel Settings { get; }

    public IReadOnlyList<ArrayBenchmarkChoice> BenchAlgorithms { get; }

    public IReadOnlyList<BenchmarkInputChoice> BenchInputOrders { get; } =
    [
        new(BenchmarkInputOrder.Random, "Случайный"),
        new(BenchmarkInputOrder.Ascending, "По возрастанию"),
        new(BenchmarkInputOrder.Descending, "По убыванию"),
    ];

    // ---------- Настройка объёмов ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint), nameof(BenchLengthsError), nameof(LengthsSummary))]
    private int? _fromLength = 100;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint), nameof(BenchLengthsError), nameof(LengthsSummary))]
    private int? _toLength = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint), nameof(BenchLengthsError), nameof(LengthsSummary))]
    private int? _stepLength = 100;

    public string BenchLengthsError => ReadLengths(out _);

    /// <summary>Короткая подпись со списком объёмов (середина сворачивается).</summary>
    public string LengthsSummary
    {
        get
        {
            if (ReadLengths(out int[] lengths).Length > 0) return "";
            if (lengths.Length <= 8)
                return $"Объёмы: {string.Join(", ", lengths)}. Всего {lengths.Length}.";
            return $"Объёмы: {lengths[0]}, {lengths[1]}, {lengths[2]} … {lengths[^2]}, {lengths[^1]}. Всего {lengths.Length}.";
        }
    }

    // ---------- Прочие параметры ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint))]
    private int? _benchRuns = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench))]
    private BenchmarkInputChoice? _benchInput;

    [ObservableProperty]
    private int? _benchSeed;

    // ---------- Результаты ----------

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<BenchmarkResult> _benchmarkResults = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBenchResults))]
    private IReadOnlyList<string> _chartCategories = [];
    public bool HasBenchResults => ChartCategories.Count > 0;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _chartSeries;

    [ObservableProperty]
    private string _benchResultSetup = "";

    [ObservableProperty]
    private double _benchProgress;

    [ObservableProperty]
    private string _benchStatus = "Замеры ещё не запускались.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench))]
    private bool _benchRunning;

    public bool CanRunBench => !BenchRunning && BenchRuns is >= 1 and <= 100 && BenchInput is not null
        && BenchAlgorithms.Any(a => a.IsSelected) && BenchLengthsError.Length == 0;

    public string BenchSetupHint
    {
        get
        {
            int algorithms = BenchAlgorithms.Count(a => a.IsSelected);
            if (algorithms == 0) return "Выберите хотя бы один алгоритм.";
            if (BenchRuns is not (>= 1 and <= 100)) return "Укажите от 1 до 100 повторов.";
            if (ReadLengths(out var lengths).Length > 0) return "";
            int total = algorithms * lengths.Length * BenchRuns!.Value;
            return $"{algorithms} × {lengths.Length} × {BenchRuns} = {total} {RunsWord(total)}";
        }
    }

    public ArrayBenchmarkViewModel(IReadOnlyList<ISortAlgorithm> algorithms, BenchmarkService benchmark, SettingsViewModel settings)
    {
        _algorithms = algorithms;
        _benchmark = benchmark;
        Settings = settings;
        BenchAlgorithms = algorithms.Select(a => new ArrayBenchmarkChoice(a)).ToArray();
        foreach (var choice in BenchAlgorithms)
            choice.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanRunBench));
                OnPropertyChanged(nameof(BenchSetupHint));
            };
        _benchInput = BenchInputOrders[0];
    }

    /// <summary>Разобрать диапазон объёмов. Возвращает текст ошибки или пустую строку.</summary>
    private string ReadLengths(out int[] lengths)
    {
        lengths = [];
        if (FromLength is not int from || ToLength is not int to || StepLength is not int step)
            return "Заполните поля «От», «До» и «Шаг».";
        if (from < MinLength || to > MaxLength)
            return $"Объёмы задаются в диапазоне от {MinLength} до {MaxLength} элементов.";
        if (to < from)
            return "Значение «До» должно быть не меньше значения «От».";
        if (step < 1)
            return "Шаг должен быть не меньше 1.";
        int count = (to - from) / step + 1;
        if (count > MaxVolumes)
            return $"Получается {count} объёмов. Увеличьте шаг или сузьте диапазон: максимум {MaxVolumes}.";

        var list = new List<int>(count);
        for (int value = from; value <= to; value += step)
            list.Add(value);
        lengths = list.ToArray();
        return "";
    }

    [RelayCommand]
    private void ResetLengths()
    {
        FromLength = 100;
        ToLength = 1000;
        StepLength = 100;
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        if (!CanRunBench || BenchRuns is not int runs || BenchInput is not { } input) return;
        if (ReadLengths(out int[] lengths).Length > 0) return;

        var algos = BenchAlgorithms.Where(a => a.IsSelected).Select(a => a.Algorithm).ToArray();
        int seed = BenchSeed ?? Random.Shared.Next();
        BenchRunning = true;
        BenchProgress = 0;
        BenchStatus = "Готовим массивы для замеров...";
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            var results = await _benchmark.RunArraysAsync(lengths, algos, runs, input.Order, seed,
                // Прогресс приходит из фонового потока, поэтому маршалим его на UI-поток.
                (pct, msg) => Dispatcher.UIThread.Post(() =>
                {
                    if (_cancellation != cancellation || cancellation.IsCancellationRequested) return;
                    BenchProgress = pct;
                    BenchStatus = msg;
                }), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            BenchmarkResults = new(results);
            ChartCategories = lengths.Select(l => l.ToString()).ToList();
            ChartSeries = results.GroupBy(r => r.Algorithm)
                .Select(group => new ChartSeriesData(group.Key, group.Select(r => r.AvgMs).ToArray()))
                .ToArray();
            BenchResultSetup = $"Порядок: {input.Name.ToLowerInvariant()} · начальное число: {seed} · {runs} {RunsWord(runs)} на объём и алгоритм";
            BenchProgress = 100;
            BenchStatus = $"Замеры завершены. Выполнено прогонов: {results.Count * runs}.";
        }
        catch (OperationCanceledException)
        {
            BenchStatus = BenchmarkResults.Count > 0
                ? "Замеры отменены. Показаны результаты предыдущего запуска."
                : "Замеры отменены.";
        }
        catch (Exception ex)
        {
            BenchStatus = "Ошибка замеров: " + ex.Message;
        }
        finally
        {
            _cancellation = null;
            BenchRunning = false;
        }
    }

    [RelayCommand]
    private void CancelBenchmark() => _cancellation?.Cancel();

    /// <summary>«прогон» / «прогона» / «прогонов» по правилам русского языка.</summary>
    private static string RunsWord(int n)
    {
        int mod100 = n % 100;
        if (n % 10 == 1 && mod100 != 11) return "прогон";
        int mod10 = n % 10;
        if (mod10 >= 2 && mod10 <= 4 && mod100 >= 12 && mod100 <= 14) return "прогонов";
        if (mod10 >= 2 && mod10 <= 4) return "прогона";
        return "прогонов";
    }
}

public sealed partial class ArrayBenchmarkChoice(ISortAlgorithm algorithm) : ObservableObject
{
    public ISortAlgorithm Algorithm { get; } = algorithm;
    [ObservableProperty] private bool _isSelected = true;
}
