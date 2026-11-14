namespace SortLab.Models;

/// <summary>Пользовательские настройки приложения. Хранятся в JSON-файле.</summary>
public sealed class AppSettings
{
    public bool DarkTheme { get; set; }
    public double FontSize { get; set; } = 14;
    public int ColorSchemeIndex { get; set; } // 0 — Обычная, 1 — Яркая, 2 — Пастельная
    public bool ShowValueLabels { get; set; } = true;
    public double DefaultDelayMs { get; set; } = 150;
}
