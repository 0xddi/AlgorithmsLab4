using System.IO;
using System.Text.Json;
using SortLab.Models;

namespace SortLab.Services;

/// <summary>Чтение/сохранение настроек приложения в JSON-файл в папке пользователя.</summary>
public sealed class SettingsService
{
    private static string FilePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SortLab");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppSettings>(json);
                if (s is not null) return s;
            }
        }
        catch
        {
            // Повреждённый файл настроек не должен ронять приложение.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, options));
        }
        catch
        {
            // Не критично: настройки просто не сохранятся.
        }
    }
}
