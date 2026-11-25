using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SortLab.Models;
using SortLab.ViewModels;

namespace SortLab.Views;

/// <summary>
/// Вкладка внешней сортировки. Колонки DataGrid строятся динамически по заголовкам CSV,
/// файловые диалоги обрабатываются в коде-файле (нужен доступ к StorageProvider).
/// </summary>
public partial class Task2View : UserControl
{
    // Желаемое смещение прокрутки лент: восстанавливаем его после пересборки
    // содержимого, иначе при каждом шаге сортировки ScrollViewer дёргается.
    private double _tapesScrollTarget = -1;

    public Task2View()
    {
        InitializeComponent();
        LegendLayout.Attach(this, LegendRail, LegendButton);
        DataContextChanged += (_, _) => WireColumns();
        TapesScroll.LayoutUpdated += (_, _) => RestoreTapesScroll();
        // Своё упорядочивание результата: встроенная сортировка DataGrid не подходит,
        // потому что колонки привязаны к индексатору записи, а не к свойствам.
        ResultGrid.AddHandler(PointerPressedEvent, OnResultHeaderPressed, RoutingStrategies.Tunnel);
    }

    /// <summary>Клик по заголовку столбца результата упорядочивает строки по этому столбцу.</summary>
    private void OnResultHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not Task2ViewModel vm || vm.ResultTable is null) return;
        if (e.GetCurrentPoint(ResultGrid).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;

        var header = (e.Source as Visual)?.GetSelfAndVisualAncestors()
            .OfType<DataGridColumnHeader>()
            .FirstOrDefault();
        if (header is null) return;

        int index = header.DataContext is DataGridColumn column ? ResultGrid.Columns.IndexOf(column) : -1;
        if (index < 0 && header.GetVisualParent() is { } parent)
            index = parent.GetVisualChildren().ToList().IndexOf(header);
        if ((uint)index >= (uint)ResultGrid.Columns.Count) return;

        vm.SortResultByColumn(index);
        UpdateResultSortArrows(vm);
        e.Handled = true;
    }

    /// <summary>Стрелка в заголовке показывает, по какому столбцу и в какую сторону упорядочен результат.</summary>
    private void UpdateResultSortArrows(Task2ViewModel vm)
    {
        if (vm.ResultTable is not { } table) return;
        for (int i = 0; i < ResultGrid.Columns.Count && i < table.Headers.Length; i++)
        {
            string header = table.Headers[i];
            if (i == vm.ResultSortColumn)
                header += vm.ResultSortDescending ? " ▼" : " ▲";
            ResultGrid.Columns[i].Header = header;
        }
    }

    private void RestoreTapesScroll()
    {
        if (_tapesScrollTarget < 0) return;
        double target = _tapesScrollTarget;
        _tapesScrollTarget = -1;
        if (Math.Abs(TapesScroll.Offset.Y - target) > 0.5)
            TapesScroll.Offset = new Vector(TapesScroll.Offset.X, target);
    }

    private void WireColumns()
    {
        if (DataContext is not Task2ViewModel vm) return;

        void BuildColumnsFor(DataGrid grid, System.Collections.Generic.IEnumerable<string>? headers)
        {
            if (headers is null) return;
            grid.Columns.Clear();
            // Шаблон заголовка с переносом: длинные названия колонок CSV не обрезаются.
            if (Application.Current?.Resources.TryGetResource("GridHeaderTemplate", null, out var headerTemplate) != true)
                headerTemplate = null;
            int i = 0;
            foreach (var h in headers)
            {
                var col = new DataGridTextColumn
                {
                    Header = h,
                    HeaderTemplate = (Avalonia.Controls.Templates.IDataTemplate?)headerTemplate,
                    Binding = new Avalonia.Data.Binding($"[{i}]"),
                    MinWidth = 108,
                    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                };
                grid.Columns.Add(col);
                i++;
            }
        }

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Task2ViewModel.Table) && vm.Table is not null)
                BuildColumnsFor(SourceGrid, vm.Table.Headers);
            if (e.PropertyName == nameof(Task2ViewModel.ResultTable) && vm.ResultTable is not null)
            {
                BuildColumnsFor(ResultGrid, vm.ResultTable.Headers);
                UpdateResultSortArrows(vm);
            }
            if (e.PropertyName == nameof(Task2ViewModel.Tapes))
            {
                // Запоминаем, где пользователь прокрутил ленты, до пересборки содержимого.
                double y = TapesScroll.Offset.Y;
                _tapesScrollTarget = y > 0 ? y : -1;
            }
        };
        if (vm.Table is not null) BuildColumnsFor(SourceGrid, vm.Table.Headers);
        if (vm.ResultTable is not null) BuildColumnsFor(ResultGrid, vm.ResultTable.Headers);
    }

    private async void OnBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || DataContext is not Task2ViewModel vm) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите CSV-файл",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("CSV / TXT") { Patterns = ["*.csv", "*.txt"] },
            ],
        });

        if (files.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            await vm.LoadFromFileAsync(path);
        }
    }

    private async void OnSaveResult(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || DataContext is not Task2ViewModel vm || vm.ResultTable is null) return;

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить отсортированную таблицу",
            SuggestedFileName = "sorted.csv",
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV") { Patterns = ["*.csv"] },
            ],
        });

        if (file is not null)
            await vm.SaveResultAsync(file.Path.LocalPath);
    }
}
