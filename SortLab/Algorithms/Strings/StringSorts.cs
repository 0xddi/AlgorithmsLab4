using SortLab.Models;

namespace SortLab.Algorithms.Strings;

public sealed class QuickSortString : IStringSortAlgorithm
{
    public string Name => "Быстрая (Quick Sort)";
    public string Reference =>
        """
        Опорное слово берём из середины участка. Его позиция = начало + целая часть ((конец − начало) / 2).

        Сравнение идёт по кодам символов до первого различия. Короткое слово предшествует своему продолжению. Слова делятся на три группы: до опорного, равные ему и после него. Только крайние группы требуют дальнейшей сортировки.
        """;
    public string Description =>
        """
        Опорное слово берём из середины текущего участка. Сравниваем слова по символам слева направо до первого различия; если одно слово целиком совпадает с началом другого, короткое идёт раньше. Здесь порядок символов определяется их кодами.

        В [dog, ant, cat, cat, fox] опорное слово cat. После разделения слева стоит ant, посередине оба cat, справа dog и fox. Снова применяем то же правило к левой и правой группам, если в них больше одного слова. Оба cat уже на своих местах. Когда весь ряд готов, одинаковые слова стоят подряд и длина каждой такой группы даёт число повторений.

        Сложность по времени (O большое, n — число слов, L — максимальная длина слова). Одно сравнение стоит до O(L). При сбалансированных разбиениях O(nL log n), в худшем случае O(n²L). Если все слова равны, хватает одного прохода O(nL).

        Устойчивость. Сортировка неустойчива. Обмены с границами групп при разбиении могут изменить относительный порядок одинаковых слов.
        """;

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots = true)
    {
        var c = new StringCtx(words) { Snapshot = snapshots };
        var ranges = new Stack<(int Left, int Right)>();
        ranges.Push((0, words.Length - 1));
        while (ranges.TryPop(out var range))
        {
            var (left, right) = range;
            if (left >= right) continue;
            IEnumerable<RangeCard> PendingCards() => ranges.Select((r, index) =>
                new RangeCard(index == 0 ? "Далее" : "Затем", r.Left, r.Right, ""));
            int pivotIndex = left + (right - left) / 2;
            string pivot = c.A[pivotIndex];
            IReadOnlyList<RangeCard>? work = snapshots
                ? [new RangeCard("Сейчас", left, right, $"Опорное слово «{pivot}».", true), .. PendingCards()]
                : null;
            c.Mark(pivotIndex, StepMark.Pivot);
            yield return c.S(SortOpType.Info, pivotIndex, -1, $"На участке от позиции {left} до {right} берём середину, позицию {pivotIndex}. Опорное слово на ней: «{pivot}».") with { WorkCards = work };
            int low = left, high = right, i = left;
            while (i <= high)
            {
                if (i != pivotIndex) c.Mark(i, StepMark.Compare);
                int order = string.CompareOrdinal(c.A[i], pivot);
                if (snapshots)
                {
                    string word = c.A[i];
                    for (int letter = 0; ; letter++)
                    {
                        bool atEnd = letter == word.Length || letter == pivot.Length;
                        bool equal = !atEnd && word[letter] == pivot[letter];
                        string reason = atEnd ? (word.Length == pivot.Length ? "Оба слова закончились одновременно и совпадают." : "Одно слово закончилось раньше и идёт первым.")
                            : equal ? "Символы одинаковы, проверяем следующую позицию."
                            : "Символы различаются, их коды определяют порядок.";
                        yield return c.S(SortOpType.Info, i, pivotIndex, $"Символ №{letter + 1}. {reason}") with
                        {
                            WorkCards = work,
                            DetailTitle = $"Посимвольное сравнение · символ №{letter + 1}",
                            Lanes = [WordLane.WithActiveLetter($"Слово с позиции {i}", word, letter),
                                WordLane.WithActiveLetter($"Опорное с позиции {pivotIndex}", pivot, letter)]
                        };
                        if (!equal) break;
                    }
                }
                string explanation = $"Сравниваем «{c.A[i]}» на позиции {i} с опорным «{pivot}» на позиции {pivotIndex}. {ExplainOrder(c.A[i], pivot, order)}";
                yield return c.S(SortOpType.Compare, i, pivotIndex, explanation) with { WorkCards = work };
                if (i != pivotIndex) c.Mark(i, StepMark.Normal);
                if (order == 0) { i++; continue; }
                int from = i;
                int to = order < 0 ? low++ : high--;
                if (order < 0) i++;
                if (from == to) continue;
                c.Swap(from, to);
                c.Mark(from, StepMark.Swap);
                c.Mark(to, StepMark.Swap);
                if (pivotIndex == from) pivotIndex = to;
                else if (pivotIndex == to) pivotIndex = from;
                c.Mark(pivotIndex, StepMark.Pivot);
                yield return c.S(SortOpType.Swap, from, to, $"Слово «{c.A[to]}» должно стоять {(order < 0 ? "до" : "после")} опорного «{pivot}». Меняем его позицию {from} с позицией {to} у границы этой группы.") with { WorkCards = work };
                c.Mark(from, StepMark.Normal);
                c.Mark(to, StepMark.Normal);
                c.Mark(pivotIndex, StepMark.Pivot);
            }
            c.MarkRange(low, high, StepMark.Sorted);
            if (high + 1 < right) ranges.Push((high + 1, right));
            if (left < low - 1) ranges.Push((left, low - 1));
            yield return c.S(SortOpType.MarkSorted, low, high, $"Слова, равные «{pivot}», на своих окончательных местах.") with
            {
                WorkCards = snapshots ? PendingCards().ToArray() : null
            };
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все слова выстроены по порядку.");
    }

    private static string ExplainOrder(string word, string pivot, int order)
    {
        if (order == 0) return "Слова совпадают. Оставляем слово в группе равных опорному.";
        int common = 0;
        while (common < word.Length && common < pivot.Length && word[common] == pivot[common])
            common++;
        string reason = common == word.Length || common == pivot.Length
            ? $"Слово «{(common == word.Length ? word : pivot)}» короче и совпадает с началом второго слова."
            : $"Первое различие у символа №{common + 1}: у символа «{word[common]}» код {(int)word[common]}, у «{pivot[common]}» код {(int)pivot[common]}.";
        return reason + (order < 0 ? " Помещаем слово в группу перед опорным." : " Помещаем слово в группу после опорного.");
    }
}

public sealed class RadixSortString : IStringSortAlgorithm
{
    public string Name => "Поразрядная (Radix Sort MSD)";
    public string Reference =>
        """
        Разряд — порядковый номер символа в слове, начиная с 1. Корзина объединяет слова с одним символом в этом разряде.

        Корзина конца слова идёт первой, остальные идут по коду символа. Следующий разряд проверяем только в корзине с несколькими незавершёнными словами.
        """;
    public string Description =>
        """
        Разряд — порядковый номер символа в слове. MSD означает, что начинаем с первого символа. Корзина: группа слов с одинаковым символом на текущей позиции. Выписываем корзины по порядку кодов символов. Следующую позицию проверяем только внутри корзины, где ещё несколько слов.

        В [bat, apple, bar, ba] сначала отделяем apple от слов на b. У оставшихся совпадает начало ba. На третьей позиции у ba уже конец слова, у bar стоит r, у bat стоит t. Корзину конца слова выписываем первой: apple, ba, bar, bat. Каждая запись слова из корзины обратно в массив даёт одну операцию размещения.

        Сложность по времени (O большое, n — число слов, L — максимальная длина слова, b — максимальное число корзин на участке). Каждое слово участвует не более чем в L + 1 разрядах, с учётом конца слова. Корзины хранятся в упорядоченном словаре. Время поиска корзины до O(log(b + 1)), вся сортировка до O(n(L + 1) log(b + 1)).

        Устойчивость. Сортировка устойчива. При обработке каждого разряда слова добавляются в корзину и извлекаются из неё в одном порядке, поэтому одинаковые слова сохраняют исходный порядок.
        """;

    public IEnumerable<StringSortStep> GetSteps(string[] words, bool snapshots = true)
    {
        var c = new StringCtx(words) { Snapshot = snapshots };
        var pending = new Stack<(int Start, int Count, int Depth)>();
        pending.Push((0, words.Length, 0));
        while (pending.TryPop(out var group))
        {
            var (start, count, depth) = group;
            if (count < 2) continue;
            IEnumerable<RangeCard> PendingCards() => pending.Select((g, index) =>
                new RangeCard(index == 0 ? "Далее" : "Затем", g.Start, g.Start + g.Count - 1, $"Символ {g.Depth + 1}."));
            IReadOnlyList<RangeCard> Work() =>
                [new RangeCard("Сейчас", start, start + count - 1, $"Символ {depth + 1}.", true), .. PendingCards()];
            var work = snapshots ? Work() : null;

            // -1 обозначает конец слова: короткое слово идёт перед своим продолжением.
            var buckets = new SortedDictionary<int, List<int>>();
            var sourceWords = c.A[start..(start + count)];
            WordLane Input(int consumed) => new("Входной участок",
                sourceWords.Skip(consumed).Select((word, i) => new WordCell(word, false, start + consumed + i)).ToArray());
            if (snapshots)
                yield return c.S(SortOpType.Info, start, -1, $"Начинаем распределение по символу №{depth + 1}.") with
                { WorkCards = work, DetailTitle = "Распределение по корзинам", Lanes = [Input(0)] };
            for (int i = start; i < start + count; i++)
            {
                var word = c.A[i];
                int key = depth == word.Length ? -1 : word[depth];
                if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = [];
                bucket.Add(i);
                if (snapshots)
                {
                    c.Mark(i, StepMark.Compare);
                    yield return c.S(SortOpType.Info, i, -1,
                        $"Слово «{word}» с позиции {i} отправляем в корзину «{BucketLabel(key)}» по символу №{depth + 1}.") with
                    {
                        WorkCards = work, DetailTitle = $"Символ {depth + 1} · распределение",
                        Lanes = [Input(i - start + 1), WordLane.WithActiveLetter($"Слово с позиции {i}", word, depth), .. ShowBuckets(buckets, sourceWords, start, key)]
                    };
                    c.Mark(i, StepMark.Normal);
                }
            }

            var buffer = new int?[count];
            WordLane ShowBuffer(int active, int consumed = 0) => new("Буфер · позиции в массиве",
                buffer.Select((id, index) => index >= consumed && id is int source
                    ? new WordCell($"{start + index}: {sourceWords[source - start]}", index == active, source)
                    : new WordCell($"{start + index}: пусто")).ToArray());
            int output = 0;
            foreach (var (key, bucket) in buckets)
            {
                foreach (var source in bucket)
                {
                    var word = sourceWords[source - start];
                    buffer[output] = source;
                    if (snapshots)
                        yield return c.S(SortOpType.Info, -1, -1, $"Переносим «{word}» из корзины «{BucketLabel(key)}» в буфер на позицию {start + output}.") with
                        {
                            WorkCards = work, DetailTitle = $"Символ {depth + 1} · сборка буфера",
                            Lanes = [ShowBuffer(output), .. ShowBuckets(buckets, sourceWords, start, key, output + 1)]
                        };
                    output++;
                }
            }

            int position = start;
            foreach (var (key, bucket) in buckets)
            {
                int first = position;
                string label = BucketLabel(key);
                for (int i = 0; i < bucket.Count; i++)
                {
                    string word = sourceWords[buffer[position - start]!.Value - start];
                    c.A[position] = word;
                    c.Mark(position, StepMark.Swap);
                    yield return c.S(SortOpType.Assign, position, -1, $"Возвращаем «{word}» из буфера на позицию {position} массива. {(key < 0 ? "Слово закончилось, дальнейших разрядов нет." : bucket.Count == 1 ? "В корзине одно слово, его место найдено." : $"В корзине «{label}» несколько слов, затем проверим символ №{depth + 2}.")}") with
                    {
                        WorkCards = work,
                        DetailTitle = snapshots ? $"Символ {depth + 1} · запись в массив" : "",
                        Lanes = snapshots ? [ShowBuffer(-1, position - start + 1),
                            new WordLane("Записано в основной массив", buffer.Take(position - start + 1)
                                .Select((id, j) => new WordCell($"{start + j}: {c.A[start + j]}", start + j == position, id)).ToArray())] : null
                    };
                    c.Mark(position++, StepMark.Normal);
                }
                if (key >= 0 && bucket.Count > 1)
                {
                    pending.Push((first, bucket.Count, depth + 1));
                    if (snapshots) work = Work();
                }
                else c.MarkRange(first, position - 1, StepMark.Sorted);
            }
            if (snapshots && pending.Count > 0)
                yield return c.S(SortOpType.Info, -1, -1, "Участок собран. Берём следующий участок с верхней карточки.")
                    with { WorkCards = PendingCards().ToArray() };
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все корзины собраны по порядку.");
    }

    private static string BucketLabel(int key) => key < 0 ? "конец слова" : $"символ «{(char)key}», код {key}";

    private static List<WordLane> ShowBuckets(SortedDictionary<int, List<int>> buckets, string[] words, int start, int active, int moved = 0)
    {
        var lanes = new List<WordLane>();
        bool distributing = moved == 0;
        foreach (var (key, indices) in buckets)
        {
            int skip = Math.Min(moved, indices.Count);
            moved -= skip;
            WordCell[] remaining = skip == indices.Count ? [new WordCell("пусто")]
                : indices.Skip(skip).Select((id, i) => new WordCell(words[id - start], distributing && key == active && i == indices.Count - 1, id)).ToArray();
            lanes.Add(new WordLane($"{(key == active ? "Текущая · " : "")}{BucketLabel(key)}", remaining));
        }
        return lanes;
    }
}
