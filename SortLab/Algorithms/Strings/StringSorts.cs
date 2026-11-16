using SortLab.Models;

namespace SortLab.Algorithms.Strings;

public sealed class QuickSortString : IStringSortAlgorithm
{
    public string Name => "Быстрая (Quick Sort)";
    public string Description => "Собираем слова в три группы: раньше опорного, равные ему и позже него.";

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots = true)
    {
        var c = new StringCtx(words) { Snapshot = snapshots };
        var ranges = new Stack<(int Left, int Right)>();
        ranges.Push((0, words.Length - 1));
        while (ranges.TryPop(out var range))
        {
            var (left, right) = range;
            if (left >= right) continue;
            int middle = left + (right - left) / 2;
            string pivot = c.A[middle];
            yield return c.S(SortOpType.Info, middle, -1, $"Опорное слово — «{pivot}». Соберём одинаковые слова вместе.");
            int low = left, high = right, i = left;
            while (i <= high)
            {
                c.Mark(i, StepMark.Compare);
                yield return c.S(SortOpType.Compare, i, -1, $"Сравниваем «{c.A[i]}» с опорным словом «{pivot}».");
                c.Mark(i, StepMark.Normal);
                int order = string.CompareOrdinal(c.A[i], pivot);
                if (order == 0) { i++; continue; }
                int from = i;
                int to = order < 0 ? low++ : high--;
                if (order < 0) i++;
                if (from == to) continue;
                c.Swap(from, to);
                c.Mark(from, StepMark.Swap);
                c.Mark(to, StepMark.Swap);
                yield return c.S(SortOpType.Swap, from, to, $"Меняем «{c.A[from]}» и «{c.A[to]}» местами.");
                c.Mark(from, StepMark.Normal);
                c.Mark(to, StepMark.Normal);
            }
            c.MarkRange(low, high, StepMark.Sorted);
            ranges.Push((high + 1, right));
            ranges.Push((left, low - 1));
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все слова выстроены по порядку.");
    }
}

public sealed class RadixSortString : IStringSortAlgorithm
{
    public string Name => "Поразрядная (Radix Sort MSD)";
    public string Description => "Раскладываем слова по первой букве, затем каждую группу — по следующей.";

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots = true)
    {
        var c = new StringCtx(words) { Snapshot = snapshots };
        var pending = new Stack<(int Start, int Count, int Depth)>();
        pending.Push((0, words.Length, 0));
        while (pending.TryPop(out var group))
        {
            var (start, count, depth) = group;
            if (count < 2) continue;

            // -1 обозначает конец слова: короткое слово идёт перед своим продолжением.
            var buckets = new SortedDictionary<int, List<string>>();
            for (int i = start; i < start + count; i++)
            {
                var word = c.A[i];
                int key = depth == word.Length ? -1 : word[depth];
                if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = [];
                bucket.Add(word);
            }

            int position = start;
            foreach (var (key, bucket) in buckets)
            {
                int first = position;
                string label = key < 0 ? "конец слова" : $"буква «{(char)key}»";
                yield return c.S(SortOpType.Info, first, -1, $"Корзина «{label}», слов: {bucket.Count}.");
                foreach (var word in bucket)
                {
                    c.A[position] = word;
                    c.Mark(position, StepMark.Swap);
                    yield return c.S(SortOpType.Assign, position, -1, $"Ставим «{word}» на позицию {position}.");
                    c.Mark(position++, StepMark.Normal);
                }
                if (key >= 0 && bucket.Count > 1) pending.Push((first, bucket.Count, depth + 1));
                else c.MarkRange(first, position - 1, StepMark.Sorted);
            }
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все корзины собраны по порядку.");
    }
}
