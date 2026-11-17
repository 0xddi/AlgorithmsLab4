using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Algorithms.Strings;
using SortLab.Controls;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>Вкладка «Задание 3. Текст и эксперименты».</summary>
public partial class Task3ViewModel : ObservableObject
{
    private const int VisualCap = 300; // для наглядности визуализируем не более 300 слов

    private readonly IReadOnlyList<IStringSortAlgorithm> _algorithms;
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

    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<BenchmarkResult> _benchmarkResults = new();

    [ObservableProperty]
    private IReadOnlyList<string>? _chartCategories;

    [ObservableProperty]
    private IReadOnlyList<ChartSeriesData>? _chartSeries;

    [ObservableProperty]
    private bool _hasBenchResults;

    [ObservableProperty]
    private double _benchProgress;

    [ObservableProperty]
    private int _benchRuns = 3;

    /// <summary>Своя длина текста для экспериментов (вводится пользователем).</summary>
    [ObservableProperty]
    private int _customBenchLength = 300;

    /// <summary>Текущий набор длин для бенчмарка (стандартные + добавленные пользователем).</summary>
    private List<int> _benchLengths = [100, 500, 1000, 2000, 5000];

    public string BenchLengthsText => "Длины: " + string.Join(", ", _benchLengths) + " слов.";

    [ObservableProperty]
    private string _benchStatus = "Эксперименты не запускались.";

    [ObservableProperty]
    private bool _benchRunning;

    public bool CanRunBench => !BenchRunning;

    public Task3ViewModel(IReadOnlyList<IStringSortAlgorithm> algorithms, BenchmarkService benchmark, SettingsViewModel settings)
    {
        _algorithms = algorithms;
        _benchmark = benchmark;
        Settings = settings;
        Algorithms = algorithms.Select(a => (ISortAlgorithmOption)new AlgorithmOption(a)).ToList();
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

    private void PublishVisual()
    {
        if (_pendingVisual is not { } step) return;
        _pendingVisual = null;
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
            ? $"{step.IndexA} ↔ {step.IndexB}"
            : step.IndexA >= 0 ? $"{step.IndexA}" : "";
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

    [RelayCommand]
    private void AddBenchLength()
    {
        int len = Math.Clamp(CustomBenchLength, 50, 50000);
        if (len != CustomBenchLength)
        {
            CustomBenchLength = len;
            StatusMessage = "Длина должна быть от 50 до 50000 слов.";
            return;
        }
        StatusMessage = "";
        if (_benchLengths.Contains(len))
        {
            BenchStatus = $"Длина {len} уже есть в списке.";
            return;
        }
        _benchLengths.Add(len);
        _benchLengths.Sort();
        OnPropertyChanged(nameof(BenchLengthsText));
        BenchStatus = $"Длина {len} добавлена — запустите эксперименты заново.";
    }

    [RelayCommand]
    private void ResetBenchLengths()
    {
        _benchLengths = [100, 500, 1000, 2000, 5000];
        OnPropertyChanged(nameof(BenchLengthsText));
        BenchStatus = "Длины возвращены к стандартным (100, 500, 1000, 2000, 5000).";
    }

    [RelayCommand]
    private async Task RunBenchmarkAsync()
    {
        if (BenchRunning) return;
        BenchRunning = true;
        HasBenchResults = false;
        OnPropertyChanged(nameof(CanRunBench));
        _benchmarkCancellation = new CancellationTokenSource();
        try
        {
            int[] lengths = _benchLengths.ToArray();
            var algos = _algorithms.ToArray();
            int runs = Math.Clamp(BenchRuns, 1, 20);
            BenchProgress = 0;
            var results = await _benchmark.RunAsync(lengths, algos, runs: runs,
                // Прогресс приходит из фонового потока — маршалим на UI-поток, иначе краш.
                (pct, msg) => Dispatcher.UIThread.Post(() =>
                {
                    BenchProgress = pct;
                    BenchStatus = msg;
                }), _benchmarkCancellation.Token);

            BenchmarkResults = new(results);
            ChartCategories = lengths.Select(l => l.ToString()).ToList();
            ChartSeries = algos.Select((a, i) => new ChartSeriesData(a.Name,
                    lengths.Select(l => results.Where(r => r.Length == l && r.Algorithm == a.Name)
                        .Select(r => r.AvgMs).FirstOrDefault())
                        .ToList()))
                .ToList();
            HasBenchResults = true;
            BenchProgress = 100;
            BenchStatus = $"Эксперименты завершены: {results.Count} замеров, по {runs} {RunsWord(runs)} на каждый.";
        }
        catch (OperationCanceledException)
        {
            BenchStatus = "Эксперименты отменены.";
        }
        catch (Exception ex)
        {
            BenchStatus = "Ошибка экспериментов: " + ex.Message;
        }
        finally
        {
            _benchmarkCancellation.Dispose();
            _benchmarkCancellation = null;
            BenchRunning = false;
            OnPropertyChanged(nameof(CanRunBench));
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
