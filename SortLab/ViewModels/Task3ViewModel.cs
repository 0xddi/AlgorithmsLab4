using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Algorithms.Internal;
using SortLab.Algorithms.Strings;
using SortLab.Controls;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>Вкладка «Задание 3. Текст и эксперименты».</summary>
public partial class Task3ViewModel : ObservableObject
{
    private const int VisualCap = 300; // для наглядности визуализируем не более 300 слов

    private readonly BenchmarkService _benchmark;
    private readonly PlaybackEngine _engine = new();
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _benchmarkCancellation;
    private StepCursor<StringSortStep>? _enumerator;
    private string[] _words = [];
    private string[] _sortedWords = [];
    private bool _finished;
    private int _currentStepNo;

    public LogBox Log { get; } = new();
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private string _inputText = "";

    [ObservableProperty]
    private string _fileName = "";

    [ObservableProperty]
    private bool _ignoreCase = true;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _wordsInfo = "Слова ещё не разобраны.";

    [ObservableProperty]
    private int _generateCount = 200;

    [ObservableProperty]
    private IReadOnlyList<WordChipVm> _chips = [];

    /// <summary>Сообщение вместо визуализации (например, «слишком много слов»).</summary>
    [ObservableProperty]
    private string _visualMessage = "";

    [ObservableProperty]
    private string _explanation = "Введите текст, загрузите файл или сгенерируйте случайный текст.";

    [ObservableProperty]
    private ISortAlgorithmOption _selectedAlgorithm;

    public IReadOnlyList<ISortAlgorithmOption> Algorithms { get; }

    [ObservableProperty]
    private double _delayMs = 50;

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<WordFrequency> _frequencies = new();

    [ObservableProperty]
    private int _maxFrequency = 1;

    [ObservableProperty]
    private string _frequencyInfo = "";

    [ObservableProperty]
    private int _comparisons;

    [ObservableProperty]
    private int _moves;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    public bool CanStart => _words.Length > 0 && !IsPlaying && !IsPaused;
    public bool CanPause => IsPlaying && !IsPaused;
    public bool CanStepBack => !IsPlaying && _currentStepNo > 0;
    public bool CanStep => _words.Length > 0 && !IsPlaying;

    // ---------- Эксперименты ----------

    private const string DefaultBenchLengths = "100, 500, 1000, 2000, 5000";
    public IReadOnlyList<BenchmarkAlgorithmChoice> BenchAlgorithms { get; }
    public IReadOnlyList<BenchmarkInputChoice> BenchInputOrders { get; } =
    [
        new(BenchmarkInputOrder.Random, "Случайный"),
        new(BenchmarkInputOrder.Ascending, "По возрастанию"),
        new(BenchmarkInputOrder.Descending, "По убыванию"),
    ];

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
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint))]
    private int? _benchRuns = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench), nameof(BenchSetupHint), nameof(BenchLengthsError))]
    private string _benchLengths = DefaultBenchLengths;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench))]
    private BenchmarkInputChoice? _benchInput;

    [ObservableProperty]
    private int? _benchSeed;

    public string BenchLengthsError => ReadBenchLengths(out _);
    public string BenchSetupHint
    {
        get
        {
            int algorithms = BenchAlgorithms.Count(a => a.IsSelected);
            if (algorithms == 0) return "Выберите хотя бы один алгоритм.";
            if (BenchRuns is not (>= 1 and <= 100)) return "Укажите от 1 до 100 прогонов.";
            if (ReadBenchLengths(out var lengths).Length > 0) return "";
            int total = algorithms * lengths.Length * BenchRuns.Value;
            return $"{algorithms} × {lengths.Length} × {BenchRuns} = {total} {RunsWord(total)}";
        }
    }

    [ObservableProperty]
    private string _benchStatus = "Замеры ещё не запускались.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunBench))]
    private bool _benchRunning;

    public bool CanRunBench => !BenchRunning && BenchRuns is >= 1 and <= 100 && BenchInput is not null
        && BenchAlgorithms.Any(a => a.IsSelected) && BenchLengthsError.Length == 0;

    /// <summary>Замеры алгоритмов внутренней сортировки (второй подраздел страницы).</summary>
    public ArrayBenchmarkViewModel Arrays { get; }

    public Task3ViewModel(IReadOnlyList<IStringSortAlgorithm> algorithms, IReadOnlyList<ISortAlgorithm> arrayAlgorithms,
        BenchmarkService benchmark, SettingsViewModel settings)
    {
        _benchmark = benchmark;
        Settings = settings;
        Arrays = new ArrayBenchmarkViewModel(arrayAlgorithms, benchmark, settings);
        Algorithms = algorithms.Select(a => (ISortAlgorithmOption)new AlgorithmOption(a)).ToList();
        BenchAlgorithms = algorithms.Select(a => new BenchmarkAlgorithmChoice(a)).ToArray();
        foreach (var choice in BenchAlgorithms)
            choice.PropertyChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(CanRunBench));
                OnPropertyChanged(nameof(BenchSetupHint));
            };
        _benchInput = BenchInputOrders[0];
        _selectedAlgorithm = Algorithms[0];
        InputText = "birch apple elm apple birch apple";
        ApplyText();
        _delayMs = 50;
    }

    partial void OnDelayMsChanged(double value) => _engine.DelayMs = (int)Math.Clamp(value, 0, 2000);

    partial void OnSelectedAlgorithmChanged(ISortAlgorithmOption value) => ApplyText();

    partial void OnIgnoreCaseChanged(bool value)
    {
        // При переключении регистра переразбираем текст, если он уже был разобран.
        if (_words.Length > 0) ApplyText();
    }

    private void NotifyCanStart()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStep));
        OnPropertyChanged(nameof(CanStepBack));
    }

    /// <summary>Обёртка алгоритма для ComboBox.</summary>
    public interface ISortAlgorithmOption
    {
        string Name { get; }
        string Description { get; }
        IStringSortAlgorithm Algo { get; }
    }

    private sealed class AlgorithmOption : ISortAlgorithmOption
    {
        public AlgorithmOption(IStringSortAlgorithm algo) => Algo = algo;
        public string Name => Algo.Name;
        public string Description => Algo.Description;
        public IStringSortAlgorithm Algo { get; }
    }

    // ---------- Текст ----------

    [RelayCommand]
    private void ApplyText()
    {
        StatusMessage = "";
        _words = TextProcessor.SplitWords(InputText, IgnoreCase);
        if (_words.Length == 0)
        {
            StatusMessage = "Текст пуст или не содержит слов.";
            WordsInfo = "Слова ещё не разобраны.";
            VisualMessage = "";
            ResetRun();
            return;
        }
        WordsInfo = $"Слов: {_words.Length}.";
        VisualMessage = _words.Length > VisualCap
            ? $"Слишком много слов для визуального отображения ({_words.Length})."
            : "";
        ResetRun();
        Explanation = "Слова разобраны. Выберите алгоритм и нажмите «Старт» или «Шаг».";
        NotifyCanStart();
    }

    [RelayCommand]
    private void GenerateText()
    {
        InputText = TextGenerator.GenerateText(Math.Clamp(GenerateCount, 50, 20000));
        ApplyText();
    }

    [RelayCommand]
    private void ToggleCase() { /* учитывается при разборе */ }

    // ---------- Проигрывание ----------

    private void ResetRun()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _engine.Reset();
        _enumerator?.Dispose();
        _enumerator = null;
        _pendingVisual = null;
        _currentStepNo = 0;
        WorkCards = [];
        WordWorkStatus = "При запуске здесь появится первый участок.";
        DetailTitle = "";
        DetailLanes = [];
        HasStepDetail = false;
        _finished = false;
        Comparisons = 0;
        Moves = 0;
        ProgressText = "";
        _sortedWords = [];
        Frequencies.Clear();
        FrequencyInfo = "";
        Log.Clear();
        IsPlaying = false;
        IsPaused = false;
        Chips = string.IsNullOrEmpty(VisualMessage)
            ? _words.Select(w => new WordChipVm { Text = w, Mark = StepMark.Normal }).ToList()
            : [];
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
    }

    [RelayCommand]
    private void Start()
    {
        if (!CanStart) return;
        if (_finished) ResetRun();
        _engine.Reset();
        _engine.DelayMs = (int)Math.Clamp(DelayMs, 0, 2000);
        _cts = new CancellationTokenSource();
        _enumerator ??= CreateCursor();
        IsPaused = false;
        IsPlaying = true;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
        _ = PlayLoopAsync(_cts.Token);
    }

    [RelayCommand]
    private void Pause()
    {
        if (!CanPause) return;
        _engine.Pause();
        IsPaused = true;
        IsPlaying = false;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
    }

    [RelayCommand]
    private void Continue()
    {
        if (!IsPaused) return;
        _engine.Resume();
        IsPaused = false;
        IsPlaying = true;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
    }

    [RelayCommand]
    private void StepBack()
    {
        if (!CanStepBack || _enumerator is null) return;
        if (_currentStepNo == 1)
        {
            ResetRun();
            Explanation = "Слова разобраны. Выберите алгоритм и нажмите «Старт» или «Шаг».";
            return;
        }
        var op = _enumerator.Current.Op;
        _enumerator.MoveBack();
        Log.RemoveStep(_currentStepNo--);
        _finished = false;
        if (op == SortOpType.Compare) Comparisons--;
        if (op is SortOpType.Swap or SortOpType.Assign) Moves--;
        var step = _enumerator.Current;
        _pendingVisual = step;
        PublishVisual();
        Explanation = step.Explanation;
        ProgressText = $"Шаг {_currentStepNo}";
        _sortedWords = step.Values;
        Frequencies.Clear();
        FrequencyInfo = "";
        MaxFrequency = 1;
    }

    [RelayCommand]
    private void Step()
    {
        if (IsPlaying) return;
        if (_finished) ResetRun();
        if (_enumerator is null && _words.Length > 0)
            _enumerator ??= CreateCursor();
        if (_enumerator is null) return;
        Advance();
    }

    private StepCursor<StringSortStep> CreateCursor()
    {
        bool showWords = _words.Length <= VisualCap;
        return new(SelectedAlgorithm.Algo.GetSteps(_words, snapshots: showWords), showWords ? 64 : 1);
    }

    private bool Advance()
    {
        if (_finished || _enumerator is null) return false;
        if (_enumerator.MoveNext())
        {
            _currentStepNo++;
            if (_currentStepNo == 1) OnPropertyChanged(nameof(CanStepBack));
            ApplyStep(_enumerator.Current);
            return true;
        }
        _finished = true;
        IsPlaying = false;
        IsPaused = false;
        _engine.Resume();
        Frequencies = new(TextProcessor.CountFrequencies(_sortedWords));
        MaxFrequency = Frequencies.Count == 0 ? 1 : Frequencies.Max(f => f.Count);
        FrequencyInfo = $"Уникальных слов: {Frequencies.Count}";
        Explanation = "Готово! Слова отсортированы, частоты подсчитаны.";
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
        return false;
    }

    [RelayCommand]
    private void Reset()
    {
        ResetRun();
        Explanation = "Сброшено. Нажмите «Старт» или «Шаг».";
    }

    private async Task PlayLoopAsync(CancellationToken ct)
    {
        try
        {
            await _engine.PlayAsync(Advance, ct, PublishVisual);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    // При быстром прогоне журнал получает каждую операцию, а сцена — последний шаг кадра.
    private StringSortStep? _pendingVisual;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveWordRange))]
    private IReadOnlyList<RangeCard> _workCards = [];
    [ObservableProperty] private string _wordWorkStatus = "";
    public RangeCard? ActiveWordRange => WorkCards.Count > 0 && WorkCards[0].IsCurrent ? WorkCards[0] : null;
    [ObservableProperty] private string _detailTitle = "";
    [ObservableProperty] private IReadOnlyList<WordLane> _detailLanes = [];
    [ObservableProperty] private bool _hasStepDetail;

    private void PublishVisual()
    {
        if (_pendingVisual is not { } step) return;
        _pendingVisual = null;
        WorkCards = step.WorkCards ?? [];
        WordWorkStatus = WorkCards.Count == 0 ? "Все слова отсортированы." : "";
        DetailTitle = step.DetailTitle;
        DetailLanes = step.Lanes ?? [];
        HasStepDetail = DetailLanes.Count > 0;
        if (_words.Length <= VisualCap)
            Chips = step.Values.Zip(step.Marks, (v, m) => new WordChipVm { Text = v, Mark = m }).ToList();
    }

    private void ApplyStep(StringSortStep step)
    {
        _sortedWords = step.Values;
        _pendingVisual = step;
        if (!IsPlaying) PublishVisual();
        Explanation = step.Explanation;
        if (step.Op == SortOpType.Compare) Comparisons++;
        if (step.Op is SortOpType.Assign or SortOpType.Swap) Moves++;
        string indices = step.IndexA >= 0 && step.IndexB >= 0
            ? $"{step.IndexA + 1}, {step.IndexB + 1}"
            : step.IndexA >= 0 ? $"{step.IndexA + 1}" : "";
        string values = step.IndexA >= 0 && step.IndexB >= 0
            ? $"«{step.Values[step.IndexA]}» ↔ «{step.Values[step.IndexB]}»"
            : step.IndexA >= 0 ? $"«{step.Values[step.IndexA]}»" : "";
        Log.Add(new LogEntry
        {
            Step = _currentStepNo,
            Operation = LogBox.OpName(step.Op),
            Indices = indices,
            Values = values,
            Explanation = step.Explanation,
        });
        ProgressText = $"Шаг {_currentStepNo}";
    }

    // ---------- Эксперименты ----------

    private string ReadBenchLengths(out int[] lengths)
    {
        lengths = [];
        if (string.IsNullOrWhiteSpace(BenchLengths)) return "Укажите хотя бы один объём.";

        var values = new SortedSet<int>();
        foreach (string part in BenchLengths.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out int length) || length is < 50 or > 50000)
                return "Введите целые числа от 50 до 50 000 через запятую.";
            if (!values.Add(length))
                return $"Объём {length} указан дважды.";
        }
        lengths = values.ToArray();
        return "";
    }

    [RelayCommand]
    private void ResetBenchLengths() => BenchLengths = DefaultBenchLengths;

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        if (!CanRunBench || BenchRuns is not int runs || BenchInput is not { } input) return;
        ReadBenchLengths(out int[] lengths);
        var algos = BenchAlgorithms.Where(a => a.IsSelected).Select(a => a.Algorithm).ToArray();
        int seed = BenchSeed ?? Random.Shared.Next();
        BenchRunning = true;
        BenchProgress = 0;
        BenchStatus = "Готовим данные для замеров...";
        using var cancellation = new CancellationTokenSource();
        _benchmarkCancellation = cancellation;
        try
        {
            var results = await _benchmark.RunAsync(lengths, algos, runs, input.Order, seed,
                // Прогресс приходит из фонового потока — маршалим на UI-поток, иначе краш.
                (pct, msg) => Dispatcher.UIThread.Post(() =>
                {
                    if (_benchmarkCancellation != cancellation || cancellation.IsCancellationRequested) return;
                    BenchProgress = pct;
                    BenchStatus = msg;
                }), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();

            BenchmarkResults = new(results);
            ChartCategories = lengths.Select(l => l.ToString()).ToList();
            ChartSeries = results.GroupBy(r => r.Algorithm)
                .Select(group => new ChartSeriesData(group.Key, group.Select(r => r.AvgMs).ToArray()))
                .ToArray();
            BenchResultSetup = $"Порядок слов: {input.Name.ToLowerInvariant()} · начальное число: {seed} · {runs} {RunsWord(runs)} на объём и алгоритм";
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
            _benchmarkCancellation = null;
            BenchRunning = false;
        }
    }

    [RelayCommand]
    private void CancelBenchmark()
    {
        _benchmarkCancellation?.Cancel();
    }

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

public sealed partial class BenchmarkAlgorithmChoice(IStringSortAlgorithm algorithm) : ObservableObject
{
    public IStringSortAlgorithm Algorithm { get; } = algorithm;
    [ObservableProperty] private bool _isSelected = true;
}

public sealed record BenchmarkInputChoice(BenchmarkInputOrder Order, string Name);
