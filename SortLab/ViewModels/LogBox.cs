using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SortLab.Models;

namespace SortLab.ViewModels;

/// <summary>
/// Логика журнала операций: приём записей, фильтрация по типу/тексту, очистка.
/// Используется всеми тремя вкладками, отображается общим LogView.
/// </summary>
public partial class LogBox : ObservableObject
{
    private readonly List<LogEntry> _all = new();

    public ObservableCollection<LogEntry> FilteredEntries { get; } = new();

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private string _selectedType = "Все";

    public IReadOnlyList<string> TypeOptions { get; } =
        new[] { "Все", "Сравнение", "Перестановка", "Присваивание", "Перемещение", "Отметка", "Инфо" };

    [ObservableProperty]
    private string _countText = "0 записей";

    public LogBox()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(FilterText) or nameof(SelectedType))
                Rebuild();
        };
    }

    public void Add(LogEntry entry)
    {
        _all.Add(entry);
        UpdateCount();
        if (Matches(entry))
            FilteredEntries.Add(entry);
    }

    public void Clear()
    {
        _all.Clear();
        FilteredEntries.Clear();
        UpdateCount();
    }

    private void Rebuild()
    {
        FilteredEntries.Clear();
        foreach (var e in _all)
            if (Matches(e))
                FilteredEntries.Add(e);
    }

    private bool Matches(LogEntry e)
    {
        if (SelectedType != "Все" && e.Operation != SelectedType) return false;
        if (string.IsNullOrEmpty(FilterText)) return true;
        return e.Explanation.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
               || e.Values.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
               || e.Indices.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateCount() => CountText = $"{_all.Count} записей";

    [RelayCommand]
    private void ClearLog() => Clear();

    /// <summary>Сформировать запись журнала из шага внутренней сортировки.</summary>
    public static string OpName(SortOpType op) => op switch
    {
        SortOpType.Compare => "Сравнение",
        SortOpType.Swap => "Перестановка",
        SortOpType.Assign => "Присваивание",
        SortOpType.MarkSorted => "Отметка",
        _ => "Инфо",
    };
}
