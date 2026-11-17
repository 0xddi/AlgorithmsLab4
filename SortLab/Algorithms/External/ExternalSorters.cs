using System.Text.Json;
using SortLab.Models;

namespace SortLab.Algorithms.External;

/// <summary>Сбалансированное слияние. Пустая строка на файловой ленте разделяет серии.</summary>
public abstract class ExternalSorterBase : IExternalSorter
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public virtual bool NeedsK => false;
    protected virtual int Paths => 2;
    protected virtual bool NaturalRuns => false;

    public IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp)
    {
        var directory = Directory.CreateTempSubdirectory("SortLab-");
        try
        {
            int k = Paths;
            string[] Files(string group) => Enumerable.Range(1, k)
                .Select(i => Path.Combine(directory.FullName, $"{group}{i}")).ToArray();
            var source = Files("A");
            var target = Files("B");
            var preview = new TapePreview(cmp);
            int runs = 0;
            using (var tapes = OpenTapes(source, File.CreateText))
            {
                TableRecord? previous = null;
                foreach (var row in input)
                {
                    bool newRun = true;
                    if (NaturalRuns && previous is not null)
                    {
                        yield return preview.Compare("Поиск серий", previous, row);
                        newRun = cmp.Compare(previous, row) > 0;
                    }
                    if (newRun)
                    {
                        if (runs > 0) tapes.Items[(runs - 1) % k].WriteLine();
                        runs++;
                    }
                    int lane = (runs - 1) % k;
                    tapes.Items[lane].WriteLine(JsonSerializer.Serialize(row));
                    yield return preview.Move("Распределение", source[lane], runs, row);
                    previous = row;
                }
                if (runs > 0) tapes.Items[(runs - 1) % k].WriteLine();
            }

            for (int pass = 1; runs > 1; pass++)
            {
                preview.Clear(target);
                int merged = 0;
                using (var readers = OpenTapes(source, File.OpenText))
                using (var writers = OpenTapes(target, File.CreateText))
                {
                    while (merged * k < runs)
                    {
                        // Одновременно держим только первую непрочитанную запись каждой серии.
                        var heads = readers.Items.Select(ReadRow).ToArray();
                        int lane = merged % k;
                        string phase = $"Слияние, проход {pass}, серия {merged + 1}";
                        while (true)
                        {
                            int best = -1;
                            for (int i = 0; i < heads.Length; i++)
                            {
                                if (heads[i] is not { } candidate) continue;
                                if (best >= 0)
                                {
                                    yield return preview.Compare(phase, heads[best]!, candidate,
                                        Path.GetFileName(source[best]), Path.GetFileName(source[i]));
                                    if (cmp.Compare(candidate, heads[best]) >= 0) continue;
                                }
                                best = i;
                            }
                            if (best < 0) break;
                            var row = heads[best]!;
                            writers.Items[lane].WriteLine(JsonSerializer.Serialize(row));
                            yield return preview.Move(phase, target[lane], merged + 1, row);
                            heads[best] = ReadRow(readers.Items[best]);
                        }
                        writers.Items[lane].WriteLine();
                        merged++;
                    }
                }
                runs = merged;
                (source, target) = (target, source);
            }

            using var resultFile = File.OpenText(source[0]);
            var result = new List<TableRecord>();
            while (ReadRow(resultFile) is { } row) result.Add(row);
            var chips = result.Select(row => Chip(row, cmp)).ToList();
            yield return new ExternalSortStep(SortOpType.Info, "Финиш",
                [new TapeState("Результат", [new SeriesBlock(chips)])],
                "Все серии слиты. В последнем файле записи идут по порядку.", "", "", result);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static TableRecord? ReadRow(StreamReader reader) =>
        reader.ReadLine() is { Length: > 0 } line ? JsonSerializer.Deserialize<TableRecord>(line) : null;

    private static ChipVm Chip(TableRecord row, RecordComparer cmp, bool compare = false, bool move = false) =>
        new() { RecordId = row.Id, Text = $"№{row.Id}: {cmp.KeyOf(row)}", IsCompared = compare, IsMoved = move };

    // В памяти остаётся только ограниченный фрагмент каждой ленты для экрана.
    // Исходные серии и результат слияния алгоритм всегда читает из файлов.
    private sealed class TapePreview(RecordComparer cmp)
    {
        private readonly Dictionary<string, List<(int Run, List<TableRecord> Rows)>> _series = new();

        public void Clear(IEnumerable<string> files)
        {
            foreach (var file in files) _series.Remove(Path.GetFileName(file));
        }

        private List<TapeState> Snapshot(int? left = null, int? right = null, int? moved = null)
        {
            return _series.Select(tape => new TapeState(tape.Key, tape.Value.Select(series =>
                new SeriesBlock(series.Rows.Select(row => Chip(row, cmp,
                    row.Id == left || row.Id == right, row.Id == moved)).ToList())).ToList())).ToList();
        }

        public ExternalSortStep Compare(string phase, TableRecord left, TableRecord right,
            string first = "Вход", string second = "Вход")
        {
            var tapes = Snapshot(left.Id, right.Id);
            tapes.Add(new TapeState($"Сравнение: {first} ↔ {second}", [new SeriesBlock(
                [Chip(left, cmp, compare: true), Chip(right, cmp, compare: true)])]));
            return new(SortOpType.Compare, phase, tapes,
                $"Сравниваем ключи «{cmp.KeyOf(left)}» и «{cmp.KeyOf(right)}» по выбранному порядку.",
                $"№{left.Id} ↔ №{right.Id}", $"{cmp.KeyOf(left)} ↔ {cmp.KeyOf(right)}");
        }

        public ExternalSortStep Move(string phase, string file, int run, TableRecord row)
        {
            string name = Path.GetFileName(file);
            if (!_series.TryGetValue(name, out var runs)) _series[name] = runs = [];
            if (runs.Count == 0 || runs[^1].Run != run)
            {
                if (runs.Count == 8) runs.RemoveAt(0);
                runs.Add((run, []));
            }
            var rows = runs[^1].Rows;
            if (rows.Count == 24) rows.RemoveAt(0);
            rows.Add(row);
            return new(SortOpType.Assign, phase, Snapshot(moved: row.Id),
                $"Записываем №{row.Id} в файл {name}, серия {run}.", $"№{row.Id} → {name}", cmp.KeyOf(row));
        }
    }

    // Закрываем также файлы, уже открытые к моменту ошибки открытия очередной ленты.
    private static TapeGroup<T> OpenTapes<T>(string[] paths, Func<string, T> open) where T : IDisposable
    {
        var group = new TapeGroup<T>();
        try
        {
            foreach (var path in paths) group.Items.Add(open(path));
            return group;
        }
        catch
        {
            group.Dispose();
            throw;
        }
    }

    private sealed class TapeGroup<T> : IDisposable where T : IDisposable
    {
        public List<T> Items { get; } = [];
        public void Dispose()
        {
            foreach (var tape in Items) tape.Dispose();
        }
    }
}

public sealed class DirectMergeSorter : ExternalSorterBase
{
    public override string Name => "Прямое слияние";
    public override string Description =>
        """
        Ключ — выбранное поле CSV, по которому сравниваем строки. Серия — последовательность строк, уже упорядоченных по ключу. Вначале каждая строка образует серию длины 1. Лента здесь означает временный файл, из которого строки читаются по порядку.

        При сортировке по возрастанию из ключей [4, 1, 3, 2] сначала получаются серии [1, 4] и [2, 3]. Сравниваем первые ещё не взятые строки двух серий и записываем меньшую целиком в выходной файл. Получаем [1, 2, 3, 4]. Когда одна серия закончилась, остаток второй дописываем без сравнения. Проходы повторяются до одной серии.
        """;
}

public sealed class NaturalMergeSorter : ExternalSorterBase
{
    public override string Name => "Естественное слияние";
    public override string Description =>
        """
        Ключ — столбец, задающий порядок строк. Ищем естественные серии: это подряд идущие строки, которые уже стоят в нужном порядке. При сортировке по возрастанию новая серия начинается там, где следующий ключ меньше предыдущего. Равные ключи остаются вместе в серии.

        Например, [2, 5, 8, 1, 6] даёт две серии: [2, 5, 8] и [1, 6]. Читаем их из временных файлов и выбираем меньший из первых оставшихся ключей: сначала 1, затем 2, 5, 6 и 8. Переносим всю строку. Чем длиннее исходные серии, тем меньше серий придётся сливать.
        """;
    protected override bool NaturalRuns => true;
}

public sealed class MultiwayMergeSorter : ExternalSorterBase
{
    public int K { get; set; } = 2;
    public override bool NeedsK => true;
    protected override int Paths => Math.Clamp(K, 2, 8);
    public override string Name => "Многопутевое слияние";
    public override string Description =>
        """
        Путь — входной временный файл. Число k задаёт, сколько серий можно сливать одновременно. Серия — группа строк, уже упорядоченная по выбранному столбцу. Начинаем с серий по одной строке. Из каждой участвующей серии читаем первую ещё не взятую строку — её называют головой.

        При k = 3 и сериях [1, 7], [3, 4], [2, 6] сравниваем головы 1, 3 и 2. Записываем строку с ключом 1; только на первом пути читаем следующую, с ключом 7. Теперь выбираем среди 7, 3 и 2. Пустые пути пропускаем. За проход объединяем до k серий в одну и повторяем до готового файла.
        """;
}
