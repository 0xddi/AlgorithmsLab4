using System.Globalization;
using SortLab.Models;

namespace SortLab.Algorithms.Strings;

/// <summary>Быстрая сортировка строк: сравниваем слова «по алфавиту».</summary>
public sealed class QuickSortString : IStringSortAlgorithm
{
    public string Name => "Быстрая (Quick Sort)";
    public string Description => "Выбираем опорное слово и раскидываем слова: до него по алфавиту — налево, после — направо.";

    public IEnumerable<StringSortStep> GetSteps(string[] words) => Run(new StringCtx(words));

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots)
    {
        var ctx = new StringCtx(words) { Snapshot = snapshots };
        return Run(ctx);
    }

    private static IEnumerable<StringSortStep> Run(StringCtx c)
    {
        c.MarkAll(StepMark.Normal);
        foreach (var s in Quick(c, 0, c.N - 1))
            yield return s;
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все слова выстроены по алфавиту.");
    }

    private static IEnumerable<StringSortStep> Quick(StringCtx c, int l, int r)
    {
        if (l > r) yield break;
        if (l == r)
        {
            c.Mark(l, StepMark.Sorted);
            yield break;
        }

        string pivot = c.A[r];
        c.Mark(r, StepMark.Pivot);
        yield return c.S(SortOpType.Info, r, -1, $"Берём кусок [{l}..{r}]. Опорное слово — «{pivot}» (фиолетовое). До него по алфавиту — налево, после — направо.");

        int i = l - 1;
        for (int j = l; j < r; j++)
        {
            c.Mark(j, StepMark.Compare);
            yield return c.S(SortOpType.Compare, j, r, $"Сравниваем «{c.A[j]}» и «{pivot}» по алфавиту: «{c.A[j]}» раньше?");
            if (string.Compare(c.A[j], pivot, StringComparison.OrdinalIgnoreCase) < 0)
            {
                i++;
                if (i != j)
                {
                    c.Swap(i, j);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, j, $"«{c.A[i]}» раньше опорного — отправляем его в левую часть: меняем местами «{c.A[i]}» и «{c.A[j]}».");
                }
            }
            c.Mark(j, StepMark.Normal);
        }

        int p = i + 1;
        if (p != r)
        {
            c.Swap(p, r);
            c.Mark(p, StepMark.Swap);
            c.Mark(r, StepMark.Swap);
            yield return c.S(SortOpType.Swap, p, r, $"Ставим опорное слово «{c.A[p]}» ровно на место: меняем местами «{c.A[p]}» и «{c.A[r]}».");
        }
        c.Mark(p, StepMark.Sorted);
        c.Mark(r, StepMark.Normal);
        yield return c.S(SortOpType.MarkSorted, p, -1, $"Опорное «{c.A[p]}» встало на позицию {p}.");

        foreach (var s in Quick(c, l, p - 1)) yield return s;
        foreach (var s in Quick(c, p + 1, r)) yield return s;
    }
}

/// <summary>Поразрядная (MSD radix) сортировка строк: раскладываем слова по корзинам букв за буквой.</summary>
public sealed class RadixSortString : IStringSortAlgorithm
{
    public string Name => "Поразрядная (Radix Sort MSD)";
    public string Description => "Смотрим на первую букву, раскладываем слова по корзинам, внутри каждой корзины — по второй букве и так далее.";

    public IEnumerable<StringSortStep> GetSteps(string[] words) => Run(new StringCtx(words));

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots)
    {
        var ctx = new StringCtx(words) { Snapshot = snapshots };
        return Run(ctx);
    }

    private static IEnumerable<StringSortStep> Run(StringCtx c)
    {
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, "Поразрядная сортировка: сортируем слова по буквам, как в словаре — сначала первая буква, потом вторая...");
        foreach (var s in Msd(c, 0, c.N - 1, 0, 1))
            yield return s;
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все корзины собраны по порядку — слова по алфавиту.");
    }

    private static char CharAt(string s, int d) => d < s.Length ? s[d] : '\0';

    private static IEnumerable<StringSortStep> Msd(StringCtx c, int lo, int hi, int d, int depth)
    {
        if (hi <= lo) yield break;

        // Мелкие группы сортируем простыми вставками — меньше возни с корзинами.
        if (hi - lo + 1 <= 8)
        {
            for (int i = lo + 1; i <= hi; i++)
            {
                int j = i;
                while (j > lo)
                {
                    c.Mark(j - 1, StepMark.Compare);
                    c.Mark(j, StepMark.Compare);
                    yield return c.S(SortOpType.Compare, j - 1, j, $"Сравниваем «{c.A[j - 1]}» и «{c.A[j]}» по алфавиту (буква №{depth}).");
                    if (string.Compare(c.A[j - 1], c.A[j], StringComparison.OrdinalIgnoreCase) > 0)
                    {
                        c.Swap(j - 1, j);
                        c.Mark(j - 1, StepMark.Swap);
                        c.Mark(j, StepMark.Swap);
                        yield return c.S(SortOpType.Swap, j - 1, j, $"«{c.A[j]}» раньше «{c.A[j - 1]}» — меняем их местами.");
                        j--;
                    }
                    else break;
                }
            }
            c.MarkRange(lo, hi, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, lo, -1, $"Маленькая группа [{lo}..{hi}] отсортирована простыми сравнениями.");
            yield break;
        }

        // Распределение по корзинам: символ на позиции d (короткое слово «пустая буква» идёт первым).
        int lo2 = lo, hi2 = hi;

        // Собираем частоты.
        var counts = new int[256];
        for (int i = lo2; i <= hi2; i++) counts[CharAt(c.A[i], d)]++;

        // Вычисляем границы корзин.
        var starts = new int[256];
        int sum = lo2;
        for (int b = 0; b < 256; b++) { starts[b] = sum; sum += counts[b]; }

        yield return c.S(SortOpType.Info, lo2, hi2,
            $"Группа из {hi2 - lo2 + 1} слов: раскладываем их по корзинам по {depth}-й букве (для коротких слов — «конец слова» первым).");

        // Раскладываем по корзинам и сразу расставляем (простой вариант: группируем и ставим на места).
        var buckets = new List<(char ch, List<int> idx)>();
        var grouped = new Dictionary<char, List<int>>();
        for (int i = lo2; i <= hi2; i++)
        {
            char ch = CharAt(c.A[i], d);
            if (!grouped.TryGetValue(ch, out var list)) grouped[ch] = list = new List<int>();
            list.Add(i);
        }
        foreach (var b in grouped.OrderBy(g => g.Key))
            buckets.Add((b.Key, b.Value));

        // Записываем слова обратно по корзинам.
        var temp = new string[hi2 - lo2 + 1];
        int pos = 0;
        foreach (var (ch, idxs) in buckets)
        {
            string show = ch == '\0' ? "конец слова" : $"'{ch}'";
            yield return c.S(SortOpType.Info, lo2 + pos, -1,
                $"Корзина {show}: в ней {idxs.Count} слов(а). Ставим их в начало группы.");
            foreach (var src in idxs)
            {
                temp[pos] = c.A[src];
                pos++;
            }
        }
        for (int i = 0; i < temp.Length; i++) c.A[lo2 + i] = temp[i];

        for (int i = lo2; i <= hi2; i++)
        {
            c.Mark(i, StepMark.Swap);
            yield return c.S(SortOpType.Assign, i, -1, $"Ставим «{c.A[i]}» на позицию {i}.");
        }
        c.MarkRange(lo2, hi2, StepMark.Normal);

        // Рекурсивно сортируем каждую корзину по следующей букве.
        foreach (var (ch, idxs) in buckets)
        {
            int start = starts[ch];
            int end = start + idxs.Count - 1;
            string show = ch == '\0' ? "конец слова" : $"'{ch}'";
            if (idxs.Count <= 1)
            {
                yield return c.S(SortOpType.Info, start, -1,
                    $"В корзине {show} всего одно слово — оно уже на месте.");
                continue;
            }

            // Защита от бесконечной рекурсии: если все слова в корзине одинаковые,
            // сортировать нечего (иначе корзина «конец слова» рекурсировала бы вечно).
            bool allEqual = true;
            for (int i = 1; i < idxs.Count; i++)
            {
                if (!string.Equals(c.A[start], c.A[start + i], StringComparison.OrdinalIgnoreCase))
                {
                    allEqual = false;
                    break;
                }
            }
            if (allEqual || d > 512)
            {
                c.MarkRange(start, end, StepMark.Sorted);
                yield return c.S(SortOpType.MarkSorted, start, -1,
                    $"Все слова в корзине {show} одинаковые — они уже на месте.");
                continue;
            }

            yield return c.S(SortOpType.Info, start, end,
                $"Внутри корзины {show} слова различаются дальше — сортируем по {depth + 1}-й букве.");
            foreach (var s in Msd(c, start, end, d + 1, depth + 1))
                yield return s;
        }
    }
}
