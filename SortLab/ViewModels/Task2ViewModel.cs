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
    private List<TableRecord>? _workingRows; // записи после фильтра (их и сортируем)
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
    private int _selectedSeparatorIndex; // 0 — авто (по файлу), 1 — запятая, 2 — точка с запятой, 3 — Tab

    /// <summary>Описание фактически используемого разделителя.</summary>
    [ObservableProperty]
    private string _separatorInfo = "Разделитель определится по файлу.";

    [ObservableProperty]
    private string? _selectedKeyColumn;

    [ObservableProperty]
    private int _selectedKeyTypeIndex; // 0 — авто, 1 — число, 2 — строка

    public KeyType SelectedKeyType => (KeyType)Math.Clamp(SelectedKeyTypeIndex, 0, 2);

    [ObservableProperty]
    private int _selectedDirectionIndex; // 0 — возрастание, 1 — убывание

    /// <summary>Фильтр записей перед сортировкой: 0 — без фильтра, иначе индекс колонки из FilterOptions − 1.</summary>
    [ObservableProperty]
    private int _filterColumnIndex;

    /// <summary>Значение фильтра (точное совпадение без учёта регистра).</summary>
    [ObservableProperty]
    private string _filterValue = "";

    /// <summary>Выбранный пример таблицы из ТЗ (индекс комбобокса).</summary>
    [ObservableProperty]
    private int _selectedSampleIndex;

    [ObservableProperty]
    private int _selectedSorterIndex;

    [ObservableProperty]
    private int _multiwayK = 2;

    public IReadOnlyList<string> SorterNames { get; }
    public IReadOnlyList<string> KeyTypes { get; } = new[] { "Автоопределение", "Число", "Строка" };
    public IReadOnlyList<string> Directions { get; } = new[] { "По возрастанию", "По убыванию" };

    // ---------- Примеры таблиц из ТЗ ----------

    /// <summary>Названия примеров для комбобокса (порядок совпадает с _sampleFiles).</summary>
    public IReadOnlyList<string> SampleNames { get; } =
    [
        "Страны: страна, континент, столица, площадь, население",
        "Химические вещества: класс, название, масса",
        "Русские слова (один атрибут)",
    ];

    private static readonly string[] _sampleFiles = ["strany.csv", "veschestva.csv", "slova.csv"];

    /// <summary>Варианты фильтра: «без фильтра» + все колонки таблицы.</summary>
    public IReadOnlyList<string> FilterOptions =>
        new[] { "(без фильтра)" }.Concat(Table?.Headers ?? Enumerable.Empty<string>()).ToArray();

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

    partial void OnTableChanged(CsvTable? value)
    {
        NotifyCanStart();
        OnPropertyChanged(nameof(FilterOptions));
        FilterColumnIndex = 0;
    }

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
        // При смене разделителя (в том числе на «авто») пробуем перечитать файл.
        if (!string.IsNullOrEmpty(_loadedPath) && File.Exists(_loadedPath))
            _ = LoadFromFileAsync(_loadedPath);
    }

    /// <summary>Ручной выбор разделителя по индексу комбобокса (0 — авто, решается отдельно).</summary>
    private static char ManualSeparator(int index) => index switch
    {
        1 => ',',
        2 => ';',
        3 => '\t',
        _ => ',',
    };

    private static string DescribeSeparator(char sep, bool auto) => (sep, auto) switch
    {
        (';', false) => "; (точка с запятой)",
        ('\t', false) => "Tab",
        (';', true) => "; (точка с запятой) — определён по файлу",
        ('\t', true) => "Tab — определён по файлу",
        (_, true) => ", (запятая) — определён по файлу",
        _ => ", (запятая)",
    };

    public async Task LoadFromFileAsync(string path)
    {
        StatusMessage = "";
        try
        {
            var auto = SelectedSeparatorIndex == 0;
            var (table, sep) = await Task.Run(() =>
            {
                char s = auto ? CsvService.DetectSeparator(path) : ManualSeparator(SelectedSeparatorIndex);
                return (_csv.Load(path, s), s);
            });
            if (table.Rows.Count > 3000)
                throw new InvalidDataException("Слишком большой файл (>3000 записей) для наглядной демонстрации.");

            Table = table;
            SourcePath = path;
            _loadedPath = path;
            FileName = System.IO.Path.GetFileName(path);
            SeparatorInfo = "Разделитель: " + DescribeSeparator(sep, auto);
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
        Log.Clear();
        Tapes = [];
        PhaseText = "";
        Explanation = "Выберите ключевой атрибут и запустите сортировку.";
        IsPlaying = false;
        IsPaused = false;
        OnPropertyChanged(nameof(CanPause));
        NotifyCanStart();
    }

    /// <summary>Загрузить пример таблицы из папки Samples (файлы копируются рядом с программой).</summary>
    [RelayCommand]
    private async Task LoadSampleAsync()
    {
        int index = Math.Clamp(SelectedSampleIndex, 0, _sampleFiles.Length - 1);
        var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Samples");
        var path = System.IO.Path.Combine(dir, _sampleFiles[index]);
        if (!File.Exists(path))
        {
            StatusMessage = $"Файл примера не найден: {path}";
            return;
        }
        await LoadFromFileAsync(path);
        StatusMessage = $"Загружен пример «{SampleNames[index]}».";
    }

    /// <summary>
    /// Отбор записей перед сортировкой (примеры 1 и 2 из ТЗ: «заданного континента»,
    /// «указанного класса»). Совпадение по колонке — точное, без учёта регистра.
    /// Возвращает null, если фильтр ничего не отобрал (запуск сортировки отменяется).
    /// </summary>
    public List<TableRecord>? ApplyFilter()
    {
        if (Table is null) return null;
        var rows = Table.Rows.ToList();
        if (FilterColumnIndex <= 0 || FilterOptions.Count <= FilterColumnIndex) return rows;

        var column = Table.Headers[FilterColumnIndex - 1];
        var col = Table.IndexOf(column);
        var value = FilterValue.Trim();
        if (value.Length == 0) return rows;

        var filtered = rows.Where(r => string.Equals(r[col].Trim(), value, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filtered.Count == 0)
        {
            StatusMessage = $"Фильтр «{column} = {value}» ничего не отобрал — сортировка не запущена.";
            return null;
        }
        return filtered;
    }

    /// <summary>Описание применённого фильтра для строки статуса.</summary>
    private string FilterDescription(int taken)
    {
        if (Table is null || FilterColumnIndex <= 0 || FilterOptions.Count <= FilterColumnIndex) return "";
        var column = Table.Headers[FilterColumnIndex - 1];
        var value = FilterValue.Trim();
        if (value.Length == 0) return "";
        return $" Фильтр: {column} = {value} (отобрано {taken} из {Table.Rows.Count}).";
    }

    [RelayCommand]
    private void Start()
    {
        if (!CanStart) return;
        if (_finished) ResetRun();

        var working = ApplyFilter();
        if (working is null) return;
        _workingRows = working;

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
        _enumerator = sorter.GetSteps(_workingRows, cmp).GetEnumerator();
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
            var working = ApplyFilter();
            if (working is null) return;
            _workingRows = working;
            var cmp = new RecordComparer(
                SelectedKeyColumn!, SelectedKeyType,
                SelectedDirectionIndex == 1, Table!.Headers);
            _comparer = cmp;
            var sorter = _sorters[Math.Clamp(SelectedSorterIndex, 0, _sorters.Count - 1)];
            if (sorter is MultiwayMergeSorter multi) multi.K = Math.Clamp(MultiwayK, 2, 8);
            _enumerator = sorter.GetSteps(_workingRows, cmp).GetEnumerator();
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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Кадровый показ при нулевой задержке: шаги копятся в фоне, UI обновляется
            // не чаще ~25 кадров/с — иначе интерфейс «не отвечает».
            ExternalSortStep? last = null;
            int steps = 0, cmp = 0, mov = 0;
            while (_enumerator is not null && _enumerator.MoveNext())
            {
                _currentStepNo++;
                var step = _enumerator.Current;
                last = step;
                steps++;
                switch (step.Op)
                {
                    case SortOpType.Compare: cmp++; break;
                    case SortOpType.Assign: mov++; break;
                }
                if (_engine.DelayMs <= 0 && sw.ElapsedMilliseconds < 40 && steps < 5000) continue;

                var s = last;
                int st = steps, c = cmp, m = mov;
                last = null;
                steps = cmp = mov = 0;
                await Dispatcher.UIThread.InvokeAsync<object?>(() => { ApplyFrame(s!, st, c, m); return (object?)null; });
                sw.Restart();
                await _engine.WaitAsync(ct);
            }
            if (last is not null)
            {
                var s = last;
                int st = steps, c = cmp, m = mov;
                await Dispatcher.UIThread.InvokeAsync<object?>(() => { ApplyFrame(s!, st, c, m); return (object?)null; });
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
        if (_comparer is not null && _workingRows is not null)
        {
            var rows = _workingRows.OrderBy(r => r, _comparer).ToList();
            var result = new CsvTable { Headers = Table!.Headers, Separator = Table.Separator };
            foreach (var r in rows) result.Rows.Add(r);
            ResultTable = result;
            HasResult = true;
            ResultInfo = $"{rows.Count} записей, ключ «{_comparer.KeyColumn}», " +
                         (_comparer.Descending ? "по убыванию" : "по возрастанию") + FilterDescription(rows.Count);
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

    /// <summary>Показать кадр: состояние последнего шага + счётчики по всем шагам кадра.</summary>
    private void ApplyFrame(ExternalSortStep step, int steps, int cmp, int mov)
    {
        Tapes = CompactTapes(step.Tapes);
        PhaseText = step.Phase;
        Explanation = step.Explanation;
        Comparisons += cmp;
        Moves += mov;
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
            Explanation = steps > 1
                ? $"{step.Explanation} (итог {steps} шагов, остальные — в счётчиках)"
                : step.Explanation,
        });
    }

    private void ApplyStep(ExternalSortStep step) =>
        ApplyFrame(step, 1,
            cmp: step.Op == SortOpType.Compare ? 1 : 0,
            mov: step.Op == SortOpType.Assign ? 1 : 0);

    // ---------- Ограничение визуализации лент ----------

    /// <summary>Максимум отрезков, показываемых на одной ленте.</summary>
    private const int MaxSeriesShown = 8;
    /// <summary>Максимум записей, показываемых в одном отрезке.</summary>
    private const int MaxChipsShown = 24;

    /// <summary>
    /// Обрезаем содержимое лент для отображения: не более 8 отрезков на ленту и 24 записей
    /// в отрезке, лишнее заменяем подписью «…ещё N». Иначе при больших файлах записи
    /// растягивают ленты по горизонтали, а UI захлёбывается на тысячах визуальных элементов.
    /// </summary>
    private static IReadOnlyList<TapeState> CompactTapes(IReadOnlyList<TapeState>? tapes)
    {
        if (tapes is null || tapes.Count == 0) return tapes ?? [];
        var result = new List<TapeState>(tapes.Count);
        foreach (var tape in tapes)
        {
            IReadOnlyList<SeriesBlock> shown = tape.Series;
            if (tape.Series.Count > MaxSeriesShown)
            {
                var blocks = tape.Series.Take(MaxSeriesShown).ToList();
                blocks.Add(new SeriesBlock([new ChipVm
                {
                    Text = $"… ещё отрезков: {tape.Series.Count - MaxSeriesShown}",
                }]));
                shown = blocks;
            }
            var compact = new List<SeriesBlock>(shown.Count);
            foreach (var block in shown)
            {
                if (block.Chips.Count <= MaxChipsShown)
                {
                    compact.Add(block);
                    continue;
                }
                var head = block.Chips.Take(MaxChipsShown).ToList();
                head.Add(new ChipVm { Text = $"…+{block.Chips.Count - MaxChipsShown}" });
                compact.Add(new SeriesBlock(head));
            }
            result.Add(new TapeState(tape.Name, compact));
        }
        return result;
    }
}
