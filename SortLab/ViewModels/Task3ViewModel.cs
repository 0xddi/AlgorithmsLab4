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
    private Task? _playTask;
    private IEnumerator<StringSortStep>? _enumerator;
    private string[] _words = [];
    private string[] _visualWords = [];
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
    public bool CanStep => !IsPlaying;

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
        InputText = "the quick brown fox jumps over the lazy dog\nthe dog barks and the fox runs away\na cat and a dog meet in the park";
        _delayMs = 50;
    }

    partial void OnDelayMsChanged(double value) => _engine.DelayMs = (int)Math.Clamp(value, 0, 2000);

    partial void OnIgnoreCaseChanged(bool value)
    {
        // При переключении регистра переразбираем текст, если он уже был разобран.
        if (_words.Length > 0) ApplyText();
    }

    private void NotifyCanStart()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStep));
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
            Chips = [];
            return;
        }
        _visualWords = _words.Length > VisualCap ? _words.Take(VisualCap).ToArray() : _words;
        WordsInfo = _words.Length > VisualCap
            ? $"Слов: {_words.Length}. Для наглядности визуализируем первые {VisualCap}."
            : $"Слов: {_words.Length}.";
        Chips = _visualWords.Select(w => new WordChipVm { Text = w, Mark = StepMark.Normal }).ToList();
        Frequencies = new(TextProcessor.CountFrequencies(_words));
        FrequencyInfo = $"Уникальных слов: {Frequencies.Count}";
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
        _enumerator?.Dispose();
        _enumerator = null;
        _currentStepNo = 0;
        _finished = false;
        Comparisons = 0;
        Moves = 0;
        ProgressText = "";
        IsPlaying = false;
        IsPaused = false;
        Chips = _visualWords.Select(w => new WordChipVm { Text = w, Mark = StepMark.Normal }).ToList();
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
        _enumerator = SelectedAlgorithm.Algo.GetSteps(_visualWords).GetEnumerator();
        IsPaused = false;
        IsPlaying = true;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
        _playTask = Task.Run(() => PlayLoopAsync(_cts.Token));
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
    private void Step()
    {
        if (IsPlaying) return;
        if (_finished) ResetRun();
        if (_enumerator is null && _visualWords.Length > 0)
            _enumerator = SelectedAlgorithm.Algo.GetSteps(_visualWords).GetEnumerator();
        if (_enumerator is null) return;
        if (_enumerator.MoveNext())
        {
            _currentStepNo++;
            ApplyStep(_enumerator.Current);
        }
        else
        {
            _finished = true;
            Explanation = "Готово! Слова выстроены по алфавиту.";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _cts?.Cancel();
        _engine.Reset();
        _playTask = null;
        ResetRun();
        Explanation = "Сброшено. Нажмите «Старт» или «Шаг».";
    }

    private async Task PlayLoopAsync(CancellationToken ct)
    {
        try
        {
            while (_enumerator is not null && _enumerator.MoveNext())
            {
                _currentStepNo++;
                var step = _enumerator.Current;
                await Dispatcher.UIThread.InvokeAsync<object?>(() => { ApplyStep(step); return (object?)null; });
                await _engine.WaitAsync(ct);
            }
            await Dispatcher.UIThread.InvokeAsync<object?>(() =>
            {
                _finished = true;
                Explanation = "Готово! Слова выстроены по алфавиту.";
                IsPlaying = false;
                IsPaused = false;
                OnPropertyChanged(nameof(CanPause));
                NotifyCanStart();
                return (object?)null;
            });
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyStep(StringSortStep step)
    {
        Chips = step.Values.Zip(step.Marks, (v, m) => new WordChipVm { Text = v, Mark = m }).ToList();
        Explanation = step.Explanation;
        switch (step.Op)
        {
            case SortOpType.Compare: Comparisons++; break;
            case SortOpType.Swap:
            case SortOpType.Assign: Moves++; break;
        }
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
    private async Task RunBenchmarkAsync()
    {
        if (BenchRunning) return;
        BenchRunning = true;
        HasBenchResults = false;
        OnPropertyChanged(nameof(CanRunBench));
        _cts = new CancellationTokenSource();
        try
        {
            int[] lengths = [100, 500, 1000, 2000, 5000];
            var algos = _algorithms.ToArray();
            int runs = Math.Clamp(BenchRuns, 1, 20);
            BenchProgress = 0;
            var results = await _benchmark.RunAsync(lengths, algos, runs: runs,
                // Прогресс приходит из фонового потока — маршалим на UI-поток, иначе краш.
                (pct, msg) => Dispatcher.UIThread.Post(() =>
                {
                    BenchProgress = pct;
                    BenchStatus = msg;
                }), _cts.Token);

            BenchmarkResults = new(results);
            ChartCategories = lengths.Select(l => l.ToString()).ToList();
            ChartSeries = algos.Select((a, i) => new ChartSeriesData(a.Name,
                    lengths.Select(l => results.Where(r => r.Length == l && r.Algorithm == a.Name)
                        .Select(r => r.AvgMs).FirstOrDefault())
                        .Select(v => v == 0 ? 0.1 : v) // нулевые времена не рисуем
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
            BenchRunning = false;
            OnPropertyChanged(nameof(CanRunBench));
        }
    }

    [RelayCommand]
    private void CancelBenchmark()
    {
        _cts?.Cancel();
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
