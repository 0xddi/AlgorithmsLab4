namespace SortLab.Models;

/// <summary>Пользовательские настройки приложения. Хранятся в JSON-файле.</summary>
public sealed class AppSettings
{
    public bool DarkTheme { get; set; }
    public int ColorSchemeIndex { get; set; } // 0 — Обычная, 1 — Яркая, 2 — Пастельная
    public bool ShowValueLabels { get; set; } = true;
    public double? MovementPercent { get; set; }

    // Читаем длительность из настроек предыдущей версии.
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? MoveDurationMs { get; set; }
    public double DefaultDelayMs { get; set; } = 150;
}
