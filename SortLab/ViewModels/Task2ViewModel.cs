using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Algorithms.External;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>Вкладка «Задание 2. Внешняя сортировка».</summary>
public partial class Task2ViewModel : ObservableObject
{
    private readonly IReadOnlyList<IExternalSorter> _sorters;
    private readonly CsvService _csv;
    private readonly PlaybackEngine _engine = new();
    private CancellationTokenSource? _cts;
    private Task? _playTask;
    private IEnumerator<ExternalSortStep>? _enumerator;
    private RecordComparer? _comparer;
    private bool _finished;
    private int _currentStepNo;
    private string? _loadedPath;

    public LogBox Log { get; } = new();
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private CsvTable? _table;

    [ObservableProperty]
    private string _fileName = "Файл не загружен";

    [ObservableProperty]
    private string _sourcePath = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private int _selectedSeparatorIndex; // 0 — запятая, 1 — точка с запятой

    [ObservableProperty]
    private string? _selectedKeyColumn;

    [ObservableProperty]
    private int _selectedKeyTypeIndex; // 0 — авто, 1 — число, 2 — строка

    public KeyType SelectedKeyType => (KeyType)Math.Clamp(SelectedKeyTypeIndex, 0, 2);

    [ObservableProperty]
    private int _selectedDirectionIndex; // 0 — возрастание, 1 — убывание

    [ObservableProperty]
    private int _selectedSorterIndex;

    [ObservableProperty]
    private int _multiwayK = 2;

    public IReadOnlyList<string> SorterNames { get; }
    public IReadOnlyList<string> KeyTypes { get; } = new[] { "Автоопределение", "Число", "Строка" };
    public IReadOnlyList<string> Directions { get; } = new[] { "По возрастанию", "По убыванию" };

    public bool IsMultiway => _sorters.Count > 0 && _sorters[Math.Clamp(SelectedSorterIndex, 0, _sorters.Count - 1)].NeedsK;

    [ObservableProperty]
    private IReadOnlyList<TapeState> _tapes = [];

    [ObservableProperty]
    private string _phaseText = "";

    [ObservableProperty]
    private string _explanation = "Загрузите CSV-файл, выберите ключ и метод сортировки.";

    [ObservableProperty]
    private double _delayMs = 100;

    [ObservableProperty]
    private CsvTable? _resultTable;

    [ObservableProperty]
    private string _resultInfo = "";

    [ObservableProperty]
    private bool _hasResult;

    [ObservableProperty]
    private int _comparisons;

    [ObservableProperty]
    private int _moves;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPaused;

    public bool CanStart => Table is not null && SelectedKeyColumn is not null && !IsPlaying && !IsPaused;
    public bool CanPause => IsPlaying && !IsPaused;
    public bool CanStep => !IsPlaying;

    partial void OnSelectedSorterIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsMultiway));
        NotifyCanStart();
    }

    partial void OnTableChanged(CsvTable? value) => NotifyCanStart();

    partial void OnSelectedKeyColumnChanged(string? value) => NotifyCanStart();

    private void NotifyCanStart()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanStep));
    }

    partial void OnDelayMsChanged(double value) => _engine.DelayMs = (int)Math.Clamp(value, 0, 2000);

    public Task2ViewModel(IReadOnlyList<IExternalSorter> sorters, CsvService csv, SettingsViewModel settings)
    {
        _sorters = sorters;
        _csv = csv;
        Settings = settings;
        SorterNames = sorters.Select(s => s.Name).ToList();
        _selectedSorterIndex = 0;
        _delayMs = 100;
    }

    partial void OnSelectedSeparatorIndexChanged(int value)
    {
        // При смене разделителя пробуем перечитать файл.
        if (!string.IsNullOrEmpty(_loadedPath) && File.Exists(_loadedPath))
            _ = LoadFromFileAsync(_loadedPath);
    }

    public async Task LoadFromFileAsync(string path)
    {
        StatusMessage = "";
        try
        {
            char sep = SelectedSeparatorIndex == 0 ? ',' : ';';
            var table = await Task.Run(() => _csv.Load(path, sep));
            if (table.Rows.Count > 3000)
                throw new InvalidDataException("Слишком большой файл (>3000 записей) для наглядной демонстрации.");

            Table = table;
            SourcePath = path;
            _loadedPath = path;
            FileName = System.IO.Path.GetFileName(path);
            var firstHeader = table.Headers.FirstOrDefault() ?? "";
            SelectedKeyColumn = firstHeader;
            OnPropertyChanged(nameof(CanStart));
            Explanation = $"Загружено {table.Rows.Count} записей, {table.Headers.Length} колонок. Выберите ключевой атрибут и метод.";
            ResultTable = null;
            HasResult = false;
            ResultInfo = "";
            ResetRun();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            StatusMessage = "Ошибка чтения файла: " + ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = "Неожиданная ошибка: " + ex.Message;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _cts?.Cancel();
        _engine.Reset();
        _playTask = null;
        ResetRun();
    }

    private void ResetRun()
    {
        _enumerator?.Dispose();
        _enumerator = null;
        _comparer = null;
        _currentStepNo = 0;
        _finished = false;
        Comparisons = 0;
        Moves = 0;
        Tapes = [];
        PhaseText = "";
        Explanation = "Выберите ключевой атрибут и запустите сортировку.";
        IsPlaying = false;
        IsPaused = false;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
    }

    [RelayCommand]
    private void Start()
    {
        if (!CanStart) return;
        if (_finished) ResetRun();

        var cmp = new RecordComparer(
            SelectedKeyColumn!, SelectedKeyType,
            SelectedDirectionIndex == 1, Table!.Headers);
        _comparer = cmp;

        // Для многопутевого слияния задаём число путей.
        var sorter = _sorters[Math.Clamp(SelectedSorterIndex, 0, _sorters.Count - 1)];
        if (sorter is MultiwayMergeSorter multi) multi.K = Math.Clamp(MultiwayK, 2, 8);

        _engine.Reset();
        _engine.DelayMs = (int)Math.Clamp(DelayMs, 0, 2000);
        _cts = new CancellationTokenSource();
        _enumerator = sorter.GetSteps(Table.Rows.ToList(), cmp).GetEnumerator();
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
        if (!CanStart && _enumerator is null) return;
        if (_finished) ResetRun();
        if (_enumerator is null)
        {
            var cmp = new RecordComparer(
                SelectedKeyColumn!, SelectedKeyType,
                SelectedDirectionIndex == 1, Table!.Headers);
            _comparer = cmp;
            var sorter = _sorters[Math.Clamp(SelectedSorterIndex, 0, _sorters.Count - 1)];
            if (sorter is MultiwayMergeSorter multi) multi.K = Math.Clamp(MultiwayK, 2, 8);
            _enumerator = sorter.GetSteps(Table.Rows.ToList(), cmp).GetEnumerator();
        }
        if (_enumerator.MoveNext())
        {
            _currentStepNo++;
            ApplyStep(_enumerator.Current);
        }
        else
        {
            _finished = true;
            Finish();
        }
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
                Finish();
                return (object?)null;
            });
        }
        catch (OperationCanceledException)
        {
            // Нормальная отмена.
        }
    }

    private void Finish()
    {
        Explanation = "Готово! Файл отсортирован. Можно сохранить результат в новый CSV-файл.";
        IsPlaying = false;
        IsPaused = false;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
        if (_comparer is not null && Table is not null)
        {
            var rows = Table.Rows.OrderBy(r => r, _comparer).ToList();
            var result = new CsvTable { Headers = Table.Headers, Separator = Table.Separator };
            foreach (var r in rows) result.Rows.Add(r);
            ResultTable = result;
            HasResult = true;
            ResultInfo = $"{rows.Count} записей, ключ «{_comparer.KeyColumn}», " + (_comparer.Descending ? "по убыванию" : "по возрастанию");
        }
    }

    /// <summary>Сохранить результат в выбранный файл.</summary>
    public async Task SaveResultAsync(string path)
    {
        if (ResultTable is null) return;
        try
        {
            await Task.Run(() => _csv.Save(path, ResultTable, ResultTable.Rows));
            StatusMessage = "Результат сохранён: " + path;
            ResultInfo = $"Сохранено: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка сохранения: " + ex.Message;
        }
    }

    private void ApplyStep(ExternalSortStep step)
    {
        Tapes = step.Tapes;
        PhaseText = step.Phase;
        Explanation = step.Explanation;
        switch (step.Op)
        {
            case SortOpType.Compare: Comparisons++; break;
            case SortOpType.Assign: Moves++; break;
        }
        Log.Add(new LogEntry
        {
            Step = _currentStepNo,
            Operation = step.Op switch
            {
                SortOpType.Compare => "Сравнение",
                SortOpType.Assign => "Перемещение",
                _ => "Инфо",
            },
            Indices = step.LogIndices,
            Values = step.LogValues,
            Explanation = step.Explanation,
        });
    }
}
