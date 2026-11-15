using System.Diagnostics;
using Avalonia.Threading;
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
    private Task? _playTask;
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
        _cts = new CancellationTokenSource();
        if (_finished)
        {
            // Прошлый прогон завершён — начинаем заново.
            ResetInternal();
        }
        if (_enumerator is null && _initialArray.Length > 0)
            _enumerator = SelectedAlgorithm.GetSteps(_initialArray).GetEnumerator();
        if (_enumerator is null) return;
        IsPaused = false;
        IsPlaying = true;
        UpdateStateFlags();
        _playTask = Task.Run(() => PlayLoopAsync(_cts.Token));
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
        if (_enumerator.MoveNext())
        {
            _currentStepNo++;
            _finished = false;
            ApplyStep(_enumerator.Current);
        }
        else
        {
            _finished = true;
            Explanation = "Готово! Сортировка завершена.";
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _cts?.Cancel();
        _engine.Reset();
        _playTask = null;
        ResetInternal();
    }

    private void ResetInternal()
    {
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Кадровый показ при нулевой задержке: шаги копятся в фоне, UI обновляется
            // не чаще ~25 кадров/с — иначе интерфейс «не отвечает».
            SortStep? last = null;
            int steps = 0, cmp = 0, swp = 0, asg = 0;
            while (_enumerator is not null && _enumerator.MoveNext())
            {
                _currentStepNo++;
                var step = _enumerator.Current;
                last = step;
                steps++;
                switch (step.Op)
                {
                    case SortOpType.Compare: cmp++; break;
                    case SortOpType.Swap: swp++; break;
                    case SortOpType.Assign: asg++; break;
                }
                if (_engine.DelayMs <= 0 && sw.ElapsedMilliseconds < 40 && steps < 5000) continue;

                var s = last;
                int st = steps, c = cmp, w = swp, a = asg;
                last = null;
                steps = cmp = swp = asg = 0;
                await Dispatcher.UIThread.InvokeAsync<object?>(() => { ApplyFrame(s!, st, c, w, a); return (object?)null; });
                sw.Restart();
                await _engine.WaitAsync(ct);
            }
            if (last is not null)
            {
                var s = last;
                int st = steps, c = cmp, w = swp, a = asg;
                await Dispatcher.UIThread.InvokeAsync<object?>(() => { ApplyFrame(s!, st, c, w, a); return (object?)null; });
            }
            await Dispatcher.UIThread.InvokeAsync<object?>(() =>
            {
                Explanation = "Готово! Сортировка завершена.";
                IsPlaying = false;
                IsPaused = false;
                _finished = true;
                UpdateStateFlags();
                return (object?)null;
            });
        }
        catch (OperationCanceledException)
        {
            // Отмена — нормальная ситуация (Сброс/пауза-отмена).
        }
    }

    /// <summary>Показать кадр: состояние последнего шага + счётчики по всем шагам кадра.</summary>
    private void ApplyFrame(SortStep step, int steps, int cmp, int swp, int asg)
    {
        Bars = step.Marks.Zip(step.Values, (m, v) => new SortBar(v, m)).ToList();
        Explanation = step.Explanation;
        Comparisons += cmp;
        Swaps += swp;
        Assignments += asg;
        string indices = step.IndexA >= 0 && step.IndexB >= 0
            ? $"{step.IndexA} ↔ {step.IndexB}"
            : step.IndexA >= 0 ? $"{step.IndexA}" : "";
        string values = step.IndexA >= 0 && step.IndexB >= 0
            ? $"{step.Values[step.IndexA]} ↔ {step.Values[step.IndexB]}"
            : step.IndexA >= 0 ? $"{step.Values[step.IndexA]}" : "";
        Log.Add(new LogEntry
        {
            Step = _currentStepNo,
            Operation = LogBox.OpName(step.Op),
            Indices = indices,
            Values = values,
            Explanation = steps > 1
                ? $"{step.Explanation} (итог {steps} шагов, остальные — в счётчиках)"
                : step.Explanation,
        });
        ProgressText = _totalSteps > 0
            ? $"Шаг {_currentStepNo} из {_totalSteps}"
            : $"Шаг {_currentStepNo}";
    }

    private void ApplyStep(SortStep step) =>
        ApplyFrame(step, 1,
            cmp: step.Op == SortOpType.Compare ? 1 : 0,
            swp: step.Op == SortOpType.Swap ? 1 : 0,
            asg: step.Op == SortOpType.Assign ? 1 : 0);
}
