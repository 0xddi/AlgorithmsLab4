namespace SortLab.Models;

/// <summary>Тип ключа для сравнения записей внешней сортировки.</summary>
public enum KeyType
{
    Auto,
    Number,
    String,
}

/// <summary>Одна запись таблицы (строка CSV). Доступ к значению — по индексу колонки.</summary>
public sealed class TableRecord
{
    /// <summary>Номер строки в исходном файле (с 1).</summary>
    public int Id { get; init; }
    public string[] Values { get; init; } = Array.Empty<string>();

    public string this[int index] => index >= 0 && index < Values.Length ? Values[index] : "";
}

/// <summary>Загруженная таблица: заголовки + строки.</summary>
public sealed class CsvTable
{
    public string[] Headers { get; init; } = Array.Empty<string>();
    public System.Collections.ObjectModel.ObservableCollection<TableRecord> Rows { get; } = new();
    public char Separator { get; init; } = ',';

    public int IndexOf(string header) => Array.IndexOf(Headers, header);
}

/// <summary>Сравнение записей по выбранному ключу.</summary>
public sealed class RecordComparer : IComparer<TableRecord>
{
    public RecordComparer(string keyColumn, KeyType type, bool descending, string[] headers)
    {
        KeyColumn = keyColumn;
        Type = type;
        Descending = descending;
        Headers = headers;
    }

    public string KeyColumn { get; }
    public KeyType Type { get; }
    public bool Descending { get; }
    public string[] Headers { get; }

    public int Index => Array.IndexOf(Headers, KeyColumn);

    public string KeyOf(TableRecord r) => r[Index];

    public int Compare(TableRecord? x, TableRecord? y)
    {
        if (x is null || y is null) return x is null && y is null ? 0 : (x is null ? -1 : 1);
        var sx = x[Index];
        var sy = y[Index];
        int r = System.Globalization.CultureInfo.GetCultureInfo("ru-RU").CompareInfo.Compare(
            sx, sy, System.Globalization.CompareOptions.IgnoreCase);
        if (Type != KeyType.String)
        {
            bool nx = double.TryParse(sx, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var dx);
            bool ny = double.TryParse(sy, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var dy);
            if (nx && ny) r = dx.CompareTo(dy);
            else if (nx != ny) r = nx ? -1 : 1;
        }
        return Descending ? -r : r;
    }
}
