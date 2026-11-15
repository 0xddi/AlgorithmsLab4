using System.IO;
using System.Text;
using SortLab.Models;

namespace SortLab.Services;

/// <summary>Чтение и запись CSV-таблиц с настраиваемым разделителем и поддержкой кавычек.</summary>
public sealed class CsvService
{
    /// <summary>
    /// Определить разделитель по содержимому файла: считаем кандидатов (запятая, точка с запятой, Tab)
    /// в первых строках вне кавычек и выбираем самого частого.
    /// </summary>
    public static char DetectSeparator(string path)
    {
        char[] candidates = [',', ';', '\t'];
        var scores = new int[candidates.Length];
        using var reader = new StreamReader(path, Encoding.UTF8);
        string? line;
        int linesRead = 0;
        while (linesRead < 10 && (line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            linesRead++;
            bool inQuotes = false;
            foreach (var ch in line)
            {
                if (ch == '"') inQuotes = !inQuotes;
                else if (!inQuotes)
                {
                    for (int i = 0; i < candidates.Length; i++)
                        if (ch == candidates[i]) scores[i]++;
                }
            }
        }
        int best = 0;
        for (int i = 1; i < candidates.Length; i++)
            if (scores[i] > scores[best]) best = i;
        return scores[best] > 0 ? candidates[best] : ',';
    }

    public CsvTable Load(string path, char separator)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length < 2)
            throw new InvalidDataException("В файле должна быть строка заголовка и хотя бы одна строка данных.");

        var headers = ParseLine(lines[0], separator);
        if (headers.Length == 0)
            throw new InvalidDataException("Не удалось прочитать заголовок CSV.");

        var table = new CsvTable { Headers = headers, Separator = separator };
        int id = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var values = ParseLine(lines[i], separator);
            // Выравниваем длину строки по числу заголовков.
            if (values.Length < headers.Length)
            {
                var fixedValues = new string[headers.Length];
                values.CopyTo(fixedValues, 0);
                values = fixedValues;
            }
            table.Rows.Add(new TableRecord { Id = ++id, Values = values });
        }
        if (table.Rows.Count == 0)
            throw new InvalidDataException("В файле нет строк с данными.");
        return table;
    }

    public void Save(string path, CsvTable table, IReadOnlyList<TableRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(JoinLine(table.Headers.Select(h => h), table.Separator));
        foreach (var r in rows)
            sb.AppendLine(JoinLine(r.Values.Take(table.Headers.Length), table.Separator));
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static string JoinLine(IEnumerable<string> fields, char sep)
    {
        var sb = new StringBuilder();
        bool first = true;
        foreach (var f in fields)
        {
            if (!first) sb.Append(sep);
            var v = f ?? "";
            bool needQuote = v.Contains(sep) || v.Contains('"') || v.Contains('\n');
            if (needQuote) v = "\"" + v.Replace("\"", "\"\"") + "\"";
            sb.Append(v);
            first = false;
        }
        return sb.ToString();
    }

    /// <summary>Разбор строки CSV с учётом кавычек и экранирования.</summary>
    private static string[] ParseLine(string line, char sep)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == sep) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        fields.Add(sb.ToString());
        // Убираем возможные \r в конце полей.
        for (int i = 0; i < fields.Count; i++)
            fields[i] = fields[i].TrimEnd('\r').Trim();
        return fields.ToArray();
    }
}
