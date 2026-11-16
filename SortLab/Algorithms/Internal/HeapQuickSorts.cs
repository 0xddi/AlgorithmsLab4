using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Пирамидальная сортировка: строим «пирамиду» (макс-кучу) и вынимаем максимум.</summary>
public sealed class HeapSort : ISortAlgorithm
{
    public string Name => "Пирамидальная (Heap Sort)";
    public string Description => "Усовершенствованный алгоритм: строим пирамиду, где родитель больше детей, и по одному вынимаем максимум в конец.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, "Сначала наведём «пирамидальный» порядок: в пирамиде каждый родитель больше своих детей.");

        // Построение пирамиды снизу вверх.
        for (int i = c.N / 2 - 1; i >= 0; i--)
            foreach (var s in SiftDown(c, i, c.N - 1))
                yield return s;

        yield return c.S(SortOpType.Info, -1, -1, "Пирамида построена! Самое большое число стоит наверху. Начинаем вынимать его в конец.");

        // Извлечение максимума.
        for (int end = c.N - 1; end > 0; end--)
        {
            c.MarkRange(end, c.N - 1, StepMark.Sorted);
            c.Swap(0, end);
            c.Mark(0, StepMark.Swap);
            c.Mark(end, StepMark.Swap);
            yield return c.S(SortOpType.Swap, 0, end, $"Самое большое число {c.A[end]} отправляем в конец: меняем местами {c.A[0]} и {c.A[end]}.");
            c.Mark(end, StepMark.Sorted);
            if (end - 1 > 0)
                foreach (var s in SiftDown(c, 0, end - 1))
                    yield return s;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Каждый раз забирали максимум — числа выстроены по возрастанию.");
    }

    /// <summary>Просеивание: родитель опускается вниз, пока дети больше него.</summary>
    private static IEnumerable<SortStep> SiftDown(SortCtx c, int root, int end)
    {
        while (true)
        {
            int child = 2 * root + 1;
            if (child > end) yield break;

            if (child + 1 <= end)
            {
                c.Mark(child, StepMark.Compare);
                c.Mark(child + 1, StepMark.Compare);
                yield return c.S(SortOpType.Compare, child, child + 1,
                    $"Сравниваем детей {c.A[child]} и {c.A[child + 1]}: выбираем большего.");
                c.Mark(child, StepMark.Normal);
                c.Mark(child + 1, StepMark.Normal);
                if (c.A[child + 1] > c.A[child]) child++;
            }
            c.Mark(root, StepMark.Compare);
            c.Mark(child, StepMark.Compare);
            yield return c.S(SortOpType.Compare, root, child,
                $"Сравниваем родителя {c.A[root]} с выбранным ребёнком {c.A[child]}.");
            if (c.A[child] > c.A[root])
            {
                c.Swap(root, child);
                c.Mark(root, StepMark.Swap);
                c.Mark(child, StepMark.Swap);
                yield return c.S(SortOpType.Swap, root, child, $"{c.A[child]} больше {c.A[root]} — меняем их местами, чтобы восстановить пирамиду.");
                c.Mark(root, StepMark.Normal);
                root = child;
            }
            else
            {
                yield return c.S(SortOpType.Info, root, child, $"Родитель {c.A[root]} не меньше ребёнка — пирамида в порядке.");
                c.Mark(root, StepMark.Normal);
                c.Mark(child, StepMark.Normal);
                yield break;
            }
        }
    }
}

/// <summary>Быстрая сортировка (Хоара, схема Ломуто) с подсветкой опорного элемента.</summary>
public sealed class QuickSort : ISortAlgorithm
{
    public string Name => "Быстрая (Quick Sort)";
    public string Description => "Усовершенствованный алгоритм: делим массив опорным числом на «меньше» и «больше», затем сортируем половинки.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        foreach (var s in Quick(c, 0, c.N - 1))
            yield return s;
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Массив разобран на маленькие кусочки и собран обратно по порядку.");
    }

    private static IEnumerable<SortStep> Quick(SortCtx c, int l, int r)
    {
        if (l > r) yield break;
        if (l == r)
        {
            c.Mark(l, StepMark.Sorted);
            yield break;
        }

        int pivot = c.A[r];
        c.Mark(r, StepMark.Pivot);
        yield return c.S(SortOpType.Info, r, -1, $"Берём кусок [{l}..{r}]. Опорное число — {pivot} (фиолетовое). Меньшие — налево, большие — направо.");

        int i = l - 1;
        for (int j = l; j < r; j++)
        {
            c.Mark(j, StepMark.Compare);
            yield return c.S(SortOpType.Compare, j, r, $"Сравниваем {c.A[j]} с опорным {pivot}: оно меньше?");
            if (c.A[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    c.Swap(i, j);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, j, $"{c.A[i]} меньше опорного — отправляем его в левую часть: меняем местами {c.A[i]} и {c.A[j]}.");
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
            yield return c.S(SortOpType.Swap, p, r, $"Ставим опорное число ровно на место: меняем местами {c.A[p]} и {c.A[r]}.");
        }
        c.Mark(p, StepMark.Sorted);
        c.Mark(r, StepMark.Normal);
        yield return c.S(SortOpType.MarkSorted, p, -1, $"Опорное {c.A[p]} встало на позицию {p}: слева всё меньше, справа всё больше.");

        foreach (var s in Quick(c, l, p - 1)) yield return s;
        foreach (var s in Quick(c, p + 1, r)) yield return s;
    }
}
