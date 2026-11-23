using System.ComponentModel;
using Avalonia.Controls;
using SortLab.ViewModels;

namespace SortLab.Views;

public partial class BenchmarkView : UserControl
{
    private Task3ViewModel? _vm;

    public BenchmarkView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WireStatus();
        Sections.SelectionChanged += (_, _) => UpdateStatus();
    }

    // Подпись под заголовком показывает статус того подраздела, который открыт.
    private void WireStatus()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnStatusChanged;
            _vm.Arrays.PropertyChanged -= OnStatusChanged;
        }
        _vm = DataContext as Task3ViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnStatusChanged;
            _vm.Arrays.PropertyChanged += OnStatusChanged;
        }
        UpdateStatus();
    }

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Task3ViewModel.BenchStatus))
            UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_vm is null) return;
        PageStatus.Text = Sections.SelectedIndex == 1 ? _vm.Arrays.BenchStatus : _vm.BenchStatus;
    }
}
