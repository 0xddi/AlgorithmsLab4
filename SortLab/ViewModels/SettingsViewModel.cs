using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SortLab.Models;
using SortLab.Services;

namespace SortLab.ViewModels;

/// <summary>
/// Глобальные настройки: тема, размер шрифта, цветовая схема визуализации, подписи значений.
/// Изменения мгновенно применяются и сохраняются в JSON.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;

    [ObservableProperty]
    private bool _darkTheme;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptionSize))]
    private double _fontSize = 14;

    public double CaptionSize => Math.Max(12, FontSize * 0.85);

    [ObservableProperty]
    private int _colorSchemeIndex;

    [ObservableProperty]
    private bool _showValueLabels = true;

    // Кисти для кастомных визуализаторов (пересчитываются при смене темы/схемы).
    [ObservableProperty] private IBrush _barNormalBrush = new SolidColorBrush(Color.Parse("#4F6EF7"));
    [ObservableProperty] private IBrush _barCompareBrush = new SolidColorBrush(Color.Parse("#FFB300"));
    [ObservableProperty] private IBrush _barSwapBrush = new SolidColorBrush(Color.Parse("#EF5350"));
    [ObservableProperty] private IBrush _barSortedBrush = new SolidColorBrush(Color.Parse("#43A047"));
    [ObservableProperty] private IBrush _barPivotBrush = new SolidColorBrush(Color.Parse("#AB47BC"));
    [ObservableProperty] private IBrush _labelBrush = new SolidColorBrush(Color.Parse("#5A6478"));
    [ObservableProperty] private IBrush _gridBrush = new SolidColorBrush(Color.Parse("#DDE3EE"));
    [ObservableProperty] private IBrush _chartBrush1 = new SolidColorBrush(Color.Parse("#4F6EF7"));
    [ObservableProperty] private IBrush _chartBrush2 = new SolidColorBrush(Color.Parse("#FFB300"));
    [ObservableProperty] private IBrush _chartBrush3 = new SolidColorBrush(Color.Parse("#43A047"));
    [ObservableProperty] private IBrush _chartBrush4 = new SolidColorBrush(Color.Parse("#AB47BC"));

    public IReadOnlyList<string> ColorSchemes { get; } = new[] { "Обычная", "Яркая", "Пастельная" };

    public SettingsViewModel(SettingsService service, AppSettings settings)
    {
        _service = service;
        _darkTheme = settings.DarkTheme;
        _fontSize = settings.FontSize;
        _colorSchemeIndex = settings.ColorSchemeIndex;
        _showValueLabels = settings.ShowValueLabels;
        UpdateBrushes();
    }

    public AppSettings ToSettings() => new()
    {
        DarkTheme = DarkTheme,
        FontSize = FontSize,
        ColorSchemeIndex = ColorSchemeIndex,
        ShowValueLabels = ShowValueLabels,
    };

    partial void OnDarkThemeChanged(bool value) => OnAnyChanged();
    partial void OnFontSizeChanged(double value) => OnAnyChanged();
    partial void OnColorSchemeIndexChanged(int value) => OnAnyChanged();
    partial void OnShowValueLabelsChanged(bool value) => OnAnyChanged();

    private void OnAnyChanged()
    {
        UpdateBrushes();
        _service.Save(ToSettings());
    }

    /// <summary>Кисти визуализации зависят и от темы, и от выбранной цветовой схемы.</summary>
    private void UpdateBrushes()
    {
        // [схема][тёмная?] => (обычный, сравнение, перестановка, отсортирован, опорный, подписи, сетка)
        (string n, string c, string s, string so, string p, string l, string g) palette = (ColorSchemeIndex, DarkTheme) switch
        {
            (0, false) => ("#C9CAD0", "#47474D", "#986035", "#597D6E", "#755D93", "#343438", "#DADADF"),
            (0, true) => ("#74747D", "#E9E9EF", "#F1BB8E", "#94C2AE", "#CEB0E1", "#E1E1E6", "#48484D"),
            (1, false) => ("#00A8CC", "#FF8F00", "#E53935", "#7CB342", "#D81B60", "#5A6478", "#D5DBE8"),
            (1, true) => ("#22D3EE", "#FFD54F", "#FF5252", "#9CCC65", "#F06292", "#AAB3C6", "#39435A"),
            (2, false) => ("#94B3E8", "#F5D76E", "#F0A3A3", "#A8D8A8", "#C9A7D8", "#5A6478", "#D5DBE8"),
            _ => ("#A8BDF5", "#F0DE8E", "#F2BCBC", "#B9E0B9", "#D6C0E8", "#AAB3C6", "#39435A"),
        };

        BarNormalBrush = new SolidColorBrush(Color.Parse(palette.n));
        BarCompareBrush = new SolidColorBrush(Color.Parse(palette.c));
        BarSwapBrush = new SolidColorBrush(Color.Parse(palette.s));
        BarSortedBrush = new SolidColorBrush(Color.Parse(palette.so));
        BarPivotBrush = new SolidColorBrush(Color.Parse(palette.p));
        LabelBrush = new SolidColorBrush(Color.Parse(palette.l));
        GridBrush = new SolidColorBrush(Color.Parse(palette.g));
        ChartBrush1 = ColorSchemeIndex == 0 ? BarSortedBrush : BarNormalBrush;
        ChartBrush2 = BarCompareBrush;
        ChartBrush3 = BarSortedBrush;
        ChartBrush4 = BarPivotBrush;
    }
}
