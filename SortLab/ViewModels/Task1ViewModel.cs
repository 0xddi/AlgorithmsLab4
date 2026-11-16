using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Algorithms.Internal;
using SortLab.Controls;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>Вкладка «Задание 1. Внутренняя сортировка».</summary>
public partial class Task1ViewModel : ObservableObject
{
    private readonly IReadOnlyList<ISortAlgorithm> _algorithms;
    private readonly PlaybackEngine _engine = new();
    private CancellationTokenSource? _cts;
    private IEnumerator<SortStep>? _enumerator;
    private int[] _initialArray = [];
    private readonly Random _random = new();
    private readonly Stopwatch _playClock = Stopwatch.StartNew();

    public LogBox Log { get; } = new();

    public SettingsViewModel Settings { get; }

    public IReadOnlyList<ISortAlgorithm> Algorithms => _algorithms;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlgorithmDescription))]
    private ISortAlgorithm _selectedAlgorithm;

    public string AlgorithmDescription => SelectedAlgorithm.Description;

    [ObservableProperty]
    private int _arraySize = 24;

    [ObservableProperty]
    private string _manualInput = "";

    [ObservableProperty]
    private string _inputError = "";

    [ObservableProperty]
    private IReadOnlyList<SortBar> _bars = [];

    [ObservableProperty]
    private string _explanation = "Выберите алгоритм, задайте массив и нажмите «Старт» или «Шаг».";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private double _delayMs = 150;

    [ObservableProperty]
    private int _comparisons;

    [ObservableProperty]
    private int _swaps;

    [ObservableProperty]
    private int _assignments;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _silentRunText = "";

    // Состояние проигрывателя.
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    public bool CanStart => !IsPlaying && !IsPaused;
    public bool CanPause => IsPlaying && !IsPaused;
    public bool CanContinueOrStep => IsPaused || (!IsPlaying && !IsPaused);

    public Task1ViewModel(IReadOnlyList<ISortAlgorithm> algorithms, SettingsViewModel settings)
    {
        _algorithms = algorithms;
        Settings = settings;
        _selectedAlgorithm = algorithms[0];
        _delayMs = settings.ToSettings().DefaultDelayMs;
        GenerateArray();
    }

    partial void OnDelayMsChanged(double value) => _engine.DelayMs = (int)Math.Clamp(value, 0, 2000);

    private void UpdateStateFlags()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanContinueOrStep));
    }

    // ---------- Данные ----------

    [RelayCommand]
    private void GenerateArray()
    {
        var arr = new int[Math.Clamp(ArraySize, 5, 100)];
        for (int i = 0; i < arr.Length; i++) arr[i] = _random.Next(1, 100);
        ManualInput = string.Join(", ", arr);
        SetInitialArray(arr);
    }

    [RelayCommand]
    private void ApplyManual()
    {
        InputError = "";
        var parts = ManualInput.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        var arr = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out arr[i]) || arr[i] < 0 || arr[i] > 999)
            {
                InputError = $"Не удалось прочитать элемент №{i + 1}: «{parts[i]}». Введите целые числа 0–999 через запятую.";
                return;
            }
        }
        if (arr.Length < 5 || arr.Length > 100)
        {
            InputError = "Нужно от 5 до 100 чисел.";
            return;
        }
        SetInitialArray(arr);
    }

    private void SetInitialArray(int[] arr)
    {
        _initialArray = arr;
        ResetInternal();
        Bars = arr.Select(v => new SortBar(v, StepMark.Normal)).ToList();
        Explanation = "Массив готов. Нажмите «Старт» или «Шаг».";
        ProgressText = "";
        // Тихий прогон: считаем операции и время алгоритма заранее.
        var stats = SilentRunner.RunInternal(SelectedAlgorithm.GetSteps(arr));
        _totalSteps = stats.TotalSteps;
        _silentComparisons = stats.Comparisons;
        _silentSwaps = stats.Swaps;
        _silentAssignments = stats.Assignments;
        SilentRunText = $"Полный прогон без пауз: {stats.TimeMs:F1} мс, {stats.TotalSteps} шагов";
    }

    private int _totalSteps;
    private int _silentComparisons, _silentSwaps, _silentAssignments;
    private int _currentStepNo;
    private bool _finished;

    // ---------- Управление проигрыванием ----------

    [RelayCommand]
    private void Start()
    {
        if (!CanStart) return;
        _engine.Reset();
        _engine.DelayMs = (int)Math.Clamp(DelayMs, 0, 2000);
        if (_finished)
        {
            // Прошлый прогон завершён — начинаем заново.
            ResetInternal();
        }
        if (_enumerator is null && _initialArray.Length > 0)
            _enumerator = SelectedAlgorithm.GetSteps(_initialArray).GetEnumerator();
        if (_enumerator is null) return;
        _cts = new CancellationTokenSource();
        IsPaused = false;
        IsPlaying = true;
        UpdateStateFlags();
        _ = PlayLoopAsync(_cts.Token);
    }

    [RelayCommand]
    private void Pause()
    {
        if (!CanPause) return;
        _engine.Pause();
        IsPaused = true;
        IsPlaying = false;
        UpdateStateFlags();
    }

    [RelayCommand]
    private void Continue()
    {
        if (!IsPaused) return;
        _engine.Resume();
        IsPaused = false;
        IsPlaying = true;
        UpdateStateFlags();
    }

    [RelayCommand]
    private void Step()
    {
        if (IsPlaying) return;
        if (_finished)
        {
            ResetInternal();
        }
        if (_enumerator is null && _initialArray.Length > 0)
            _enumerator = SelectedAlgorithm.GetSteps(_initialArray).GetEnumerator();
        if (_enumerator is null) return;
        Advance();
    }

    private bool Advance()
    {
        if (_finished || _enumerator is null) return false;
        if (_enumerator.MoveNext())
        {
            _currentStepNo++;
            ApplyStep(_enumerator.Current);
            return true;
        }
        _enumerator.Dispose();
        _enumerator = null;
        _finished = true;
        IsPlaying = false;
        IsPaused = false;
        _engine.Resume();
        Explanation = "Готово! Сортировка завершена.";
        UpdateStateFlags();
        return false;
    }

    [RelayCommand]
    private void Reset()
    {
        ResetInternal();
    }

    private void ResetInternal()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _engine.Reset();
        _enumerator?.Dispose();
        _enumerator = null;
        _currentStepNo = 0;
        _finished = false;
        Comparisons = 0;
        Swaps = 0;
        Assignments = 0;
        IsPlaying = false;
        IsPaused = false;
        Log.Clear();
        UpdateStateFlags();
    }

    private async Task PlayLoopAsync(CancellationToken ct)
    {
        try
        {
            await _engine.PlayAsync(Advance, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    }

    private void ApplyStep(SortStep step)
    {
        Bars = step.Marks.Zip(step.Values, (m, v) => new SortBar(v, m)).ToList();
        Explanation = step.Explanation;
        if (step.Op == SortOpType.Compare) Comparisons++;
        if (step.Op == SortOpType.Swap) Swaps++;
        if (step.Op == SortOpType.Assign) Assignments++;
        string indices = step.IndexA >= 0 && step.IndexB >= 0
            ? $"{step.IndexA} ↔ {step.IndexB}"
            : step.IndexA >= 0 ? $"{step.IndexA}" : "";
        string values = step.IndexA >= 0 && step.IndexB >= 0
            ? $"{step.Values[step.IndexA]} ↔ {step.SavedKey ?? step.Values[step.IndexB]}"
            : step.IndexA >= 0 ? $"{step.Values[step.IndexA]}" : "";
        Log.Add(new LogEntry
        {
            Step = _currentStepNo,
            Operation = LogBox.OpName(step.Op),
            Indices = indices,
            Values = values,
            Explanation = step.Explanation,
        });
        ProgressText = _totalSteps > 0
            ? $"Шаг {_currentStepNo} из {_totalSteps}"
            : $"Шаг {_currentStepNo}";
    }


}
