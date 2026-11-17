using Avalonia;
using Avalonia.Controls;
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
    public Task2View()
    {
        InitializeComponent();
        LegendLayout.Attach(this, LegendRail, LegendButton);
        DataContextChanged += (_, _) => WireColumns();
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
                BuildColumnsFor(ResultGrid, vm.ResultTable.Headers);
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
