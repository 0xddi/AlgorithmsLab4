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
        int r;
        var useNumber = Type == KeyType.Number
                        || (Type == KeyType.Auto
                            && double.TryParse(sx, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out _)
                            && double.TryParse(sy, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out _));
        if (useNumber
            && double.TryParse(sx, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dx)
            && double.TryParse(sy, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dy))
        {
            r = dx.CompareTo(dy);
        }
        else
        {
            r = string.Compare(sx, sy, StringComparison.OrdinalIgnoreCase);
        }
        return Descending ? -r : r;
    }
}
