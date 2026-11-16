using System.ComponentModel;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using SortLab.Algorithms.External;
using SortLab.Algorithms.Internal;
using SortLab.Algorithms.Strings;
using SortLab.Services;
using SortLab.ViewModels;
using SortLab.Views;

namespace SortLab;

public partial class App : Application
{
    private SettingsService? _settingsService;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        _settingsService = new SettingsService();
        var settings = _settingsService.Load();

        var settingsVm = new SettingsViewModel(_settingsService, settings);
        settingsVm.PropertyChanged += OnSettingsPropertyChanged;
        ApplyTheme(settingsVm.DarkTheme);

        // Простой ручной DI: создаём сервисы и ViewModel-и здесь.
        ISortAlgorithm[] sortAlgorithms =
        {
            new ImprovedBubbleSort(),
            new InsertionSort(),
            new ShellSort(),
            new SelectionSort(),
            new HeapSort(),
            new QuickSort(),
        };

        IExternalSorter[] externalSorters =
        {
            new DirectMergeSorter(),
            new NaturalMergeSorter(),
            new MultiwayMergeSorter(),
        };

        IStringSortAlgorithm[] stringSorters =
        {
            new QuickSortString(),
            new RadixSortString(),
        };

        var task1 = new Task1ViewModel(sortAlgorithms, settingsVm);
        var task2 = new Task2ViewModel(externalSorters, new CsvService(), settingsVm);
        var task3 = new Task3ViewModel(stringSorters, new BenchmarkService(), settingsVm);

        var mainVm = new MainWindowViewModel(task1, task2, task3, settingsVm);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = mainVm };
            desktop.Exit += (_, _) => task2.ResetCommand.Execute(null);
            desktop.ShutdownRequested += (_, _) => _settingsService.Save(settingsVm.ToSettings());
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        var vm = (SettingsViewModel)sender!;
        if (e.PropertyName == nameof(SettingsViewModel.DarkTheme))
            ApplyTheme(vm.DarkTheme);
    }

    private static void ApplyTheme(bool dark) =>
        Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
}
