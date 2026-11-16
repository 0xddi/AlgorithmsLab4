using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SortLab.ViewModels;

/// <summary>Корневая ViewModel главного окна: вкладки заданий + настройки.</summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private int _selectedPageIndex;

    [RelayCommand]
    private void OpenBenchmarks() => SelectedPageIndex = 3;

    public Task1ViewModel Task1 { get; }
    public Task2ViewModel Task2 { get; }
    public Task3ViewModel Task3 { get; }
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(Task1ViewModel task1, Task2ViewModel task2, Task3ViewModel task3, SettingsViewModel settings)
    {
        Task1 = task1;
        Task2 = task2;
        Task3 = task3;
        Settings = settings;
    }
}
