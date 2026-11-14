using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SortLab.ViewModels;

namespace SortLab.Views;

public partial class Task3View : UserControl
{
    public Task3View()
    {
        InitializeComponent();
    }

    private async void OnBrowse(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null || DataContext is not Task3ViewModel vm) return;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите текстовый файл",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Текст") { Patterns = ["*.txt"] },
                new FilePickerFileType("Все файлы") { Patterns = ["*"] },
            ],
        });

        if (files.Count > 0)
        {
            try
            {
                var path = files[0].Path.LocalPath;
                vm.InputText = await System.IO.File.ReadAllTextAsync(path);
                vm.FileName = System.IO.Path.GetFileName(path);
                vm.ApplyTextCommand.Execute(null);
            }
            catch (Exception ex)
            {
                vm.StatusMessage = "Ошибка чтения файла: " + ex.Message;
            }
        }
    }
}
