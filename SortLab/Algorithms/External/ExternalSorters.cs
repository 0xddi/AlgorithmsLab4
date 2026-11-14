using SortLab.Models;

namespace SortLab.Algorithms.External;

/// <summary>
/// Базовый класс внешних сортировщиков: общие помощники для построения состояний лент.
/// Все реализации работают с записями в памяти, имитируя файлы/ленты (так нагляднее для лабы).
/// </summary>
public abstract class ExternalSorterBase : IExternalSorter
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    /// <summary>Требуется ли выбор числа путей k (только для многопутевого слияния).</summary>
    public virtual bool NeedsK => false;

    public abstract IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp);

    protected static ChipVm Chip(TableRecord r, RecordComparer cmp, bool compared = false, bool moved = false) =>
        new() { Text = $"№{r.Id}: {cmp.KeyOf(r)}", RecordId = r.Id, IsCompared = compared, IsMoved = moved };

    protected static TapeState Tape(string name, IEnumerable<IReadOnlyList<TableRecord>> runs, RecordComparer cmp,
        ISet<int> compared, ISet<int> moved)
    {
        var series = runs.Where(s => s.Count > 0)
            .Select(s => new SeriesBlock(s.Select(r => Chip(r, cmp, compared.Contains(r.Id), moved.Contains(r.Id))).ToList()))
            .ToList();
        return new TapeState(name, series);
    }

    protected static ExternalSortStep Step(SortOpType op, string phase, List<TapeState> tapes, string expl,
        string indices = "", string values = "") =>
        new(op, phase, tapes, expl, indices, values);

    protected static readonly IReadOnlyList<List<TableRecord>> Empty = new List<List<TableRecord>>();

    /// <summary>Разбить текущий файл на отрезки заданной длины.</summary>
    protected static List<List<TableRecord>> SplitRuns(IReadOnlyList<TableRecord> cur, int runLen)
    {
        var runs = new List<List<TableRecord>>();
        for (int i = 0; i < cur.Count; i += runLen)
        {
            var run = new List<TableRecord>();
            for (int j = i; j < Math.Min(i + runLen, cur.Count); j++) run.Add(cur[j]);
            runs.Add(run);
        }
        return runs;
    }

    /// <summary>Слить два отсортированных отрезка, генерируя шаги сравнения и записи.</summary>
    protected static IEnumerable<ExternalSortStep> MergeTwoRuns(
        List<TableRecord> a, List<TableRecord> b, List<TableRecord> merged,
        RecordComparer cmp, Func<List<TapeState>> snapshot, string phase)
    {
        int ia = 0, ib = 0;
        while (ia < a.Count && ib < b.Count)
        {
            yield return Step(SortOpType.Compare, phase, snapshot(),
                $"Сравниваем первые записи двух отрезков: №{a[ia].Id} ({cmp.KeyOf(a[ia])}) и №{b[ib].Id} ({cmp.KeyOf(b[ib])}).",
                $"№{a[ia].Id} ↔ №{b[ib].Id}", $"{cmp.KeyOf(a[ia])} ↔ {cmp.KeyOf(b[ib])}");
            TableRecord win;
            if (cmp.Compare(a[ia], b[ib]) <= 0) { win = a[ia]; ia++; }
            else { win = b[ib]; ib++; }
            merged.Add(win);
            yield return Step(SortOpType.Assign, phase, snapshot(),
                $"Меньшая запись №{win.Id} ({cmp.KeyOf(win)}) отправляется в выходной файл.",
                $"№{win.Id} → выход", cmp.KeyOf(win));
        }
        // Хвосты — просто переносим, сравнивать уже не с чем.
        while (ia < a.Count)
        {
            merged.Add(a[ia]);
            yield return Step(SortOpType.Assign, phase, snapshot(),
                $"Отрезок закончился — переносим остаток: №{a[ia].Id} ({cmp.KeyOf(a[ia])}) в выходной файл.",
                $"№{a[ia].Id} → выход", cmp.KeyOf(a[ia]));
            ia++;
        }
        while (ib < b.Count)
        {
            merged.Add(b[ib]);
            yield return Step(SortOpType.Assign, phase, snapshot(),
                $"Отрезок закончился — переносим остаток: №{b[ib].Id} ({cmp.KeyOf(b[ib])}) в выходной файл.",
                $"№{b[ib].Id} → выход", cmp.KeyOf(b[ib]));
            ib++;
        }
    }
}

/// <summary>Прямое слияние: отрезки фиксированной длины, удваивающейся каждую фазу.</summary>
public sealed class DirectMergeSorter : ExternalSorterBase
{
    public override string Name => "Прямое слияние";
    public override string Description => "Делим файл на отрезки по 1 записи, сливаем пары, потом отрезки по 2, 4, 8...";

    public override IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp)
    {
        var cur = input.ToList();
        int runLen = 1, phase = 1;

        yield return Step(SortOpType.Info, "Старт",
            new List<TapeState> { Tape("Вход", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()) },
            "Начинаем прямое слияние. Сначала считаем каждую запись отдельным (уже отсортированным!) отрезком длины 1.");

        while (runLen < cur.Count)
        {
            string phaseName = $"Фаза {phase}: отрезки по {runLen} → {runLen * 2}";
            var runs = SplitRuns(cur, runLen);

            // Распределение отрезков поочерёдно в файлы A и B.
            var aRuns = new List<List<TableRecord>>();
            var bRuns = new List<List<TableRecord>>();
            for (int i = 0; i < runs.Count; i++)
                (i % 2 == 0 ? aRuns : bRuns).Add(runs[i]);

            var built = new List<List<TableRecord>> { new(), new() };
            for (int i = 0; i < runs.Count; i++)
            {
                int tapeIdx = i % 2;
                string targetName = tapeIdx == 0 ? "Файл A" : "Файл B";
                foreach (var r in runs[i])
                {
                    built[tapeIdx].Add(r);
                    var tapes = new List<TapeState>
                    {
                        Tape("Вход", new[] { cur }, cmp, new HashSet<int> { r.Id }, new HashSet<int>()),
                        Tape("Файл A", SplitRuns(built[0], runLen), cmp, new HashSet<int>(), new HashSet<int> { r.Id }),
                        Tape("Файл B", SplitRuns(built[1], runLen), cmp, new HashSet<int>(), new HashSet<int> { r.Id }),
                        Tape("Файл C (выход)", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    };
                    yield return Step(SortOpType.Assign, phaseName + " — распределение", tapes,
                        $"Запись №{r.Id} ({cmp.KeyOf(r)}) отправляется в {targetName}: отрезки раскладываем поочерёдно.",
                        $"№{r.Id} → {targetName}", cmp.KeyOf(r));
                }
            }

            // Слияние пар отрезков в файл C.
            var outputRuns = new List<List<TableRecord>>();
            int maxPairs = Math.Max(aRuns.Count, bRuns.Count);
            for (int p = 0; p < maxPairs; p++)
            {
                var a = p < aRuns.Count ? aRuns[p] : new List<TableRecord>();
                var b = p < bRuns.Count ? bRuns[p] : new List<TableRecord>();
                var merged = new List<TableRecord>();
                outputRuns.Add(merged);

                List<TapeState> Snap() => new()
                {
                    Tape("Вход", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл A", aRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл B", bRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл C (выход)", outputRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                };

                foreach (var s in MergeTwoRuns(a, b, merged, cmp, Snap, phaseName + " — слияние"))
                    yield return s;
            }

            cur = outputRuns.SelectMany(x => x).ToList();
            runLen *= 2;
            phase++;
            yield return Step(SortOpType.Info, phaseName, new List<TapeState>
                {
                    Tape("Вход", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл A", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл B", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл C (выход)", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                },
                $"Фаза {phase - 1} готова: теперь файл состоит из отсортированных отрезков длиной {runLen / 2}.");
        }

        yield return Step(SortOpType.Info, "Финиш",
            new List<TapeState> { Tape("Результат", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()) },
            "Готово! Отрезки стали длиной во весь файл — файл полностью отсортирован.");
    }
}

/// <summary>Естественное слияние: работаем с уже упорядоченными «природными» отрезками.</summary>
public sealed class NaturalMergeSorter : ExternalSorterBase
{
    public override string Name => "Естественное слияние";
    public override string Description => "Сначала ищем в файле уже упорядоченные отрезки (как повезло), и сливаем именно их.";

    public override IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp)
    {
        var cur = input.ToList();
        int phase = 1;

        var initial = DetectRuns(cur, cmp).ToList();
        yield return Step(SortOpType.Info, "Старт",
            new List<TapeState> { Tape("Вход", initial, cmp, new HashSet<int>(), new HashSet<int>()) },
            $"Начинаем естественное слияние: в файле уже нашлось {initial.Count} упорядоченных отрезков — с ними и работаем.");

        var runs = initial;
        while (runs.Count > 1)
        {
            string phaseName = $"Фаза {phase}";

            // Распределение отрезков поочерёдно в A и B.
            var aRuns = new List<List<TableRecord>>();
            var bRuns = new List<List<TableRecord>>();
            var placed = new List<List<List<TableRecord>>> { aRuns, bRuns };
            for (int i = 0; i < runs.Count; i++)
                placed[i % 2].Add(runs[i]);

            var built = new List<List<TableRecord>> { new(), new() };
            for (int i = 0; i < runs.Count; i++)
            {
                int tapeIdx = i % 2;
                foreach (var r in runs[i])
                {
                    built[tapeIdx].Add(r);
                    var tapes = new List<TapeState>
                    {
                        Tape("Вход", runs, cmp, new HashSet<int> { r.Id }, new HashSet<int>()),
                        Tape("Файл A", new[] { built[0] }, cmp, new HashSet<int>(), new HashSet<int> { r.Id }),
                        Tape("Файл B", new[] { built[1] }, cmp, new HashSet<int>(), new HashSet<int> { r.Id }),
                        Tape("Файл C (выход)", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    };
                    yield return Step(SortOpType.Assign, phaseName + " — распределение", tapes,
                        $"Отрезок №{i + 1} уходит в {(tapeIdx == 0 ? "Файл A" : "Файл B")}: запись №{r.Id} ({cmp.KeyOf(r)}).",
                        $"№{r.Id} → {(tapeIdx == 0 ? "A" : "B")}", cmp.KeyOf(r));
                }
            }

            // Слияние пар отрезков.
            var outputRuns = new List<List<TableRecord>>();
            int maxPairs = Math.Max(aRuns.Count, bRuns.Count);
            for (int p = 0; p < maxPairs; p++)
            {
                var a = p < aRuns.Count ? aRuns[p] : new List<TableRecord>();
                var b = p < bRuns.Count ? bRuns[p] : new List<TableRecord>();
                var merged = new List<TableRecord>();
                outputRuns.Add(merged);

                List<TapeState> Snap() => new()
                {
                    Tape("Вход", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл A", aRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл B", bRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл C (выход)", outputRuns, cmp, new HashSet<int>(), new HashSet<int>()),
                };

                foreach (var s in MergeTwoRuns(a, b, merged, cmp, Snap, phaseName + " — слияние"))
                    yield return s;
            }

            cur = outputRuns.SelectMany(x => x).ToList();
            runs = DetectRuns(cur, cmp).ToList();
            phase++;
            yield return Step(SortOpType.Info, phaseName, new List<TapeState>
                {
                    Tape("Вход", runs, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл A", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл B", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                    Tape("Файл C (выход)", Empty, cmp, new HashSet<int>(), new HashSet<int>()),
                },
                $"Фаза {phase - 1} готова: осталось {runs.Count} упорядоченных отрезков.");
        }

        yield return Step(SortOpType.Info, "Финиш",
            new List<TapeState> { Tape("Результат", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()) },
            "Готово! Отрезков больше одного не осталось — файл полностью отсортирован.");
    }

    /// <summary>Найти «естественные» отрезки: идём по файлу, пока ключи не убывают.</summary>
    private static List<List<TableRecord>> DetectRuns(IReadOnlyList<TableRecord> cur, RecordComparer cmp)
    {
        var runs = new List<List<TableRecord>>();
        if (cur.Count == 0) return runs;
        var run = new List<TableRecord> { cur[0] };
        for (int i = 1; i < cur.Count; i++)
        {
            if (cmp.Compare(cur[i - 1], cur[i]) <= 0) run.Add(cur[i]);
            else { runs.Add(run); run = new List<TableRecord> { cur[i] }; }
        }
        runs.Add(run);
        return runs;
    }
}

/// <summary>Многопутевое слияние: отрезки раскладываются на k лент и сливаются все сразу.</summary>
public sealed class MultiwayMergeSorter : ExternalSorterBase
{
    /// <summary>Число путей k (лент). Задаётся пользователем перед запуском.</summary>
    public int K { get; set; } = 2;

    public override string Name => "Многопутевое слияние";
    public override string Description => "Делим на k лент и на каждом шаге сливаем по одному отрезку с каждой ленты.";

    public override bool NeedsK => true;

    public override IEnumerable<ExternalSortStep> GetSteps(IReadOnlyList<TableRecord> input, RecordComparer cmp)
    {
        int k = Math.Clamp(K, 2, 8);
        var runs = SplitRuns(input, 1);
        int phase = 1;
        var tapeNames = Enumerable.Range(1, k).Select(i => $"Лента {i}").ToList();
        var cur = input.ToList();

        yield return Step(SortOpType.Info, "Старт",
            new List<TapeState> { Tape("Вход", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()) },
            $"Начинаем {k}-путевое слияние: раскладываем отрезки на {k} лент и сливаем их все сразу.");

        while (runs.Count > 1)
        {
            string phaseName = $"Фаза {phase}: {runs.Count} отрезков → {Math.Max(1, (int)Math.Ceiling(runs.Count / (double)k))}";

            // Распределение по k лент поочерёдно (round-robin).
            var tapeRuns = new List<List<List<TableRecord>>>();
            for (int i = 0; i < k; i++) tapeRuns.Add(new List<List<TableRecord>>());
            for (int i = 0; i < runs.Count; i++)
                tapeRuns[i % k].Add(runs[i]);

            var built = new List<List<TableRecord>>();
            for (int i = 0; i < k; i++) built.Add(new List<TableRecord>());

            for (int i = 0; i < runs.Count; i++)
            {
                int t = i % k;
                foreach (var r in runs[i])
                {
                    built[t].Add(r);
                    var tapes = new List<TapeState> { Tape("Вход", runs, cmp, new HashSet<int> { r.Id }, new HashSet<int>()) };
                    for (int tt = 0; tt < k; tt++)
                        tapes.Add(Tape(tapeNames[tt], new[] { built[tt] }, cmp, new HashSet<int>(), new HashSet<int> { r.Id }));
                    tapes.Add(Tape("Выход", Empty, cmp, new HashSet<int>(), new HashSet<int>()));
                    yield return Step(SortOpType.Assign, phaseName + " — распределение", tapes,
                        $"Отрезок №{i + 1} уходит на {tapeNames[t]}: запись №{r.Id} ({cmp.KeyOf(r)}).",
                        $"№{r.Id} → {tapeNames[t]}", cmp.KeyOf(r));
                }
            }

            // Многопутевое слияние: по одному отрезку с каждой ленты.
            var outputRuns = new List<List<TableRecord>>();
            bool anyLeft = true;
            while (anyLeft)
            {
                anyLeft = false;
                var heads = new List<List<TableRecord>>();
                var usedTapes = new List<int>();
                for (int t = 0; t < k; t++)
                {
                    if (tapeRuns[t].Count > 0)
                    {
                        heads.Add(tapeRuns[t][0]);
                        usedTapes.Add(t);
                        anyLeft = true;
                    }
                }
                if (!anyLeft) break;

                var merged = new List<TableRecord>();
                outputRuns.Add(merged);

                List<TapeState> Snap(ISet<int> compared, ISet<int> moved)
                {
                    var tapes = new List<TapeState>();
                    for (int t = 0; t < k; t++)
                        tapes.Add(Tape(tapeNames[t], tapeRuns[t], cmp, compared, moved));
                    tapes.Add(Tape("Выход", outputRuns, cmp, compared, moved));
                    return tapes;
                }

                var cursors = heads.Select(_ => 0).ToList();
                bool active = cursors.Where((c, idx) => c < heads[idx].Count).Any();
                while (active)
                {
                    // Ищем минимум среди первых записей активных отрезков.
                    int best = -1;
                    for (int idx = 0; idx < heads.Count; idx++)
                    {
                        if (cursors[idx] >= heads[idx].Count) continue;
                        if (best == -1) { best = idx; continue; }
                        var bestRec = heads[best][cursors[best]];
                        var cand = heads[idx][cursors[idx]];
                        yield return Step(SortOpType.Compare, phaseName + " — слияние", Snap(new HashSet<int> { bestRec.Id, cand.Id }, new HashSet<int>()),
                            $"Сравниваем №{bestRec.Id} ({cmp.KeyOf(bestRec)}) и №{cand.Id} ({cmp.KeyOf(cand)}): чей ключ меньше, та запись идёт в выход.",
                            $"№{bestRec.Id} ↔ №{cand.Id}", $"{cmp.KeyOf(bestRec)} ↔ {cmp.KeyOf(cand)}");
                        if (cmp.Compare(cand, bestRec) < 0) best = idx;
                    }
                    var win = heads[best][cursors[best]];
                    cursors[best]++;
                    merged.Add(win);
                    yield return Step(SortOpType.Assign, phaseName + " — слияние", Snap(new HashSet<int>(), new HashSet<int> { win.Id }),
                        $"Запись №{win.Id} ({cmp.KeyOf(win)}) записывается в выходной файл.",
                        $"№{win.Id} → выход", cmp.KeyOf(win));
                    active = cursors.Where((c, idx) => c < heads[idx].Count).Any();
                }

                // Удаляем слитые отрезки с лент.
                foreach (var t in usedTapes) tapeRuns[t].RemoveAt(0);
            }

            cur = outputRuns.SelectMany(x => x).ToList();
            runs = outputRuns;
            phase++;
            yield return Step(SortOpType.Info, phaseName, new List<TapeState>
                {
                    Tape("Вход", runs, cmp, new HashSet<int>(), new HashSet<int>()),
                },
                $"Фаза {phase - 1} готова: осталось {runs.Count} упорядоченных отрезков.");
        }

        yield return Step(SortOpType.Info, "Финиш",
            new List<TapeState> { Tape("Результат", new[] { cur }, cmp, new HashSet<int>(), new HashSet<int>()) },
            "Готово! Отрезков больше одного не осталось — файл полностью отсортирован.");
    }
}
