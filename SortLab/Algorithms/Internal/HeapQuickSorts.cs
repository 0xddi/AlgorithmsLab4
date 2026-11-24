using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Пирамидальная сортировка: строим «пирамиду» (макс-кучу) и вынимаем максимум.</summary>
public sealed class HeapSort : ISortAlgorithm
{
    public string Name => "Пирамидальная (Heap Sort)";
    public string Reference =>
        """
        Макс-куча: каждый родитель не меньше своих детей. При просеивании выбираем большего ребёнка; при равенстве берём левого. Меняем его с родителем, только если ребёнок больше.
        """;
    public string Description =>
        """
        Представьте массив как дерево. Первая ячейка с индексом 0 — корень. У родителя с индексом p дети находятся на 2p + 1 и 2p + 2, пока эти индексы меньше размера кучи. Чтобы найти родителя, вычитаем 1 из индекса ребёнка, делим на 2 и отбрасываем остаток. Роли определяет место, а значения нужны для сравнения.

        В [4, 9, 6, 2, 7] дети числа 4 это числа 9 и 6. У числа 9 тоже есть дети: 2 и 7. В макс-куче каждый родитель не меньше своих детей. Чтобы исправить вершину, выбираем большего ребёнка: 9 поднимается вместо 4. Затем 4 сравниваем с 2 и 7 и опускаем ещё раз. Это просеивание вниз.

        Строим кучу снизу вверх. Максимум оказывается в корне. Меняем его с последним числом кучи, исключаем эту позицию и снова просеиваем корень. Так справа растёт готовая часть массива.

        Сложность по времени (O большое, n — число элементов). Построение кучи O(n), вся сортировка в худшем случае O(n log n).

        Устойчивость. Сортировка неустойчива. При обмене корня кучи с последним элементом её активной части относительный порядок равных элементов может измениться.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, c.N >= 2
            ? $"Последний элемент стоит на позиции {c.N - 1}. Позиция его родителя равна целой части (({c.N - 1} − 1) / 2), то есть {c.N / 2 - 1}. Начнём с этого родителя и пройдём к корню."
            : "В массиве нет пары для сравнения.", new HeapFrame(c.N, c.N >= 2 ? c.N / 2 - 1 : null));

        // Построение пирамиды снизу вверх.
        for (int i = c.N / 2 - 1; i >= 0; i--)
            foreach (var s in SiftDown(c, i, c.N - 1))
                yield return s;

        yield return c.S(SortOpType.Info, -1, -1, "Пирамида построена! Самое большое число стоит наверху. Начинаем вынимать его в конец.", new HeapFrame(c.N));

        // Извлечение максимума.
        for (int end = c.N - 1; end > 0; end--)
        {
            c.MarkRange(end, c.N - 1, StepMark.Sorted);
            c.Swap(0, end);
            c.Mark(0, StepMark.Swap);
            c.Mark(end, StepMark.Swap);
            yield return c.S(SortOpType.Swap, 0, end, $"Максимум {c.A[end]} занял позицию {end}. В куче осталось {end} элементов.", new HeapFrame(end));
            c.Mark(end, StepMark.Sorted);
            if (end - 1 > 0)
                foreach (var s in SiftDown(c, 0, end - 1))
                    yield return s;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Каждый раз забирали максимум, поэтому числа выстроены по возрастанию.", new HeapFrame(0));
    }

    /// <summary>Просеивание: родитель опускается вниз, пока дети больше него.</summary>
    private static IEnumerable<SortStep> SiftDown(SortCtx c, int root, int end)
    {
        while (true)
        {
            int child = 2 * root + 1;
            var frame = new HeapFrame(end + 1, root);
            c.Mark(root, StepMark.HeapParent);
            if (child > end)
            {
                yield return c.S(SortOpType.Info, root, -1,
                    $"От позиции {root} ищем левого ребёнка: 2 × {root} + 1 = {frame.Left}. Индекс не меньше размера кучи {frame.Size}, поэтому у числа {c.A[root]} нет детей.", frame);
                c.Mark(root, StepMark.Normal);
                yield break;
            }

            c.Mark(child, StepMark.HeapChild);
            bool hasRight = child + 1 <= end;
            if (hasRight) c.Mark(child + 1, StepMark.HeapChild);
            string right = hasRight ? $" (число {c.A[child + 1]})" : $" ≥ {frame.Size}, поэтому правого ребёнка нет";
            yield return c.S(SortOpType.Info, root, child,
                $"Родитель — число {c.A[root]} на позиции {root}. Левый ребёнок: 2 × {root} + 1 = {frame.Left} (число {c.A[child]}). Правый ребёнок: 2 × {root} + 2 = {frame.Right}{right}.", frame);
            if (hasRight)
            {
                yield return c.S(SortOpType.Compare, child, child + 1,
                    $"Левый ребёнок — число {c.A[child]} на позиции {child}. Правый ребёнок — число {c.A[child + 1]} на позиции {child + 1}. {(c.A[child] == c.A[child + 1] ? "Значения равны, выбираем левого ребёнка." : c.A[child] > c.A[child + 1] ? "Значение левого ребёнка больше, выбираем его." : "Значение правого ребёнка больше, выбираем его.")}", frame);
                c.Mark(child, StepMark.Normal);
                c.Mark(child + 1, StepMark.Normal);
                if (c.A[child + 1] > c.A[child]) child++;
            }
            c.Mark(child, StepMark.HeapChild);
            string childRole = child == frame.Left ? "левого ребёнка" : "правого ребёнка";
            yield return c.S(SortOpType.Compare, root, child,
                $"Для {childRole} на позиции {child} берём целую часть (({child} − 1) / 2) и получаем позицию родителя {root}. Сравниваем их значения: {c.A[child]} и {c.A[root]}.", frame);
            if (c.A[child] > c.A[root])
            {
                c.Swap(root, child);
                c.Mark(root, StepMark.Swap);
                c.Mark(child, StepMark.Swap);
                yield return c.S(SortOpType.Swap, root, child, $"Значение {childRole} ({c.A[root]}) больше значения родителя ({c.A[child]}). Меняем позиции {root} и {child}. Продолжаем с позиции {child}.", frame);
                c.Mark(root, StepMark.Normal);
                c.Mark(child, StepMark.Normal);
                root = child;
            }
            else
            {
                yield return c.S(SortOpType.Info, root, child, $"Значение родителя ({c.A[root]}) не меньше значения {childRole} ({c.A[child]}). Просеивание закончено.", frame);
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
    public string Reference =>
        """
        Опорное число: последнее на текущем участке. Граница отделяет числа строго меньше опорного от остальных.

        Место опорного = начало участка + количество меньших чисел. Равные остаются в группе «больше или равно». Оба оставшихся участка сортируются отдельно.
        """;
    public string Description =>
        """
        Опорное число: значение, с которым сравниваем весь выбранный участок. Здесь берём последнее число. Просматриваем остальные слева направо: каждое строго меньшее переносим к левой границе. Равные опорному и большие остаются в другой группе.

        В [6, 2, 4] опорное число 4. Число 2 переходит влево, затем ставим 4 между группами: [2, 4, 6]. Число 4 уже на окончательной позиции 1. Повторяем то же правило отдельно слева и справа — это рекурсия. Участок длиной 0 или 1 уже готов. Части могут быть разной длины.

        Сложность по времени (O большое, n — число элементов). При сбалансированных разбиениях и в среднем для случайного порядка различных чисел O(n log n). В худшем случае O(n²), например при уже упорядоченном массиве или когда все числа равны.

        Устойчивость. Сортировка неустойчива. При установке опорного элемента на его место он может оказаться перед равным элементом, который изначально стоял раньше.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input, bool snapshots)
    {
        var c = new SortCtx(input) { Snapshots = snapshots };
        c.MarkAll(StepMark.Normal);
        foreach (var s in Quick(c, 0, c.N - 1))
            yield return s;
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Массив разобран на маленькие кусочки и собран обратно по порядку.");
    }

    private static IEnumerable<SortStep> Quick(SortCtx c, int l, int r, RangeFrame? caller = null)
    {
        if (l > r) yield break;
        var call = new RangeFrame(l, r, caller);
        if (l == r)
        {
            c.Mark(l, StepMark.Sorted);
            yield return c.S(SortOpType.Info, l, -1, $"На позиции {l} остался один элемент. Возвращаемся из вызова.")
                with { Call = call with { Detail = $"Здесь только число {c.A[l]}. Участок готов." } };
            yield break;
        }

        int pivot = c.A[r];
        call = call with { Detail = $"Разделяем участок вокруг опорного числа {pivot}." };
        c.Mark(r, StepMark.Pivot);
        yield return c.S(SortOpType.Info, r, -1, $"Берём позиции с {l} по {r}. Опорное число: последнее на этом участке, {pivot}. В начале участка собираем числа строго меньше опорного.") with { Call = call };

        int i = l - 1;
        for (int j = l; j < r; j++)
        {
            c.Mark(j, StepMark.Compare);
            yield return c.S(SortOpType.Compare, j, r, $"Число {c.A[j]} на позиции {j} сравниваем с опорным {pivot}. {(c.A[j] < pivot ? "Оно меньше, поэтому расширяем группу меньших чисел." : "Оно больше или равно опорному, поэтому граница группы меньших чисел остаётся на месте.")}") with { Call = call };
            if (c.A[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    c.Swap(i, j);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, j, $"{c.A[i]} меньше опорного, переносим его с позиции {j} на позицию {i}, следующую в группе меньших чисел.") with { Call = call };
                }
                c.Mark(i, StepMark.LessThanPivot);
            }
            if (j > i) c.Mark(j, StepMark.AtLeastPivot);
        }

        int p = i + 1;
        if (p != r)
        {
            c.Swap(p, r);
            c.Mark(p, StepMark.Swap);
            c.Mark(r, StepMark.Swap);
            yield return c.S(SortOpType.Swap, p, r, $"В участке {p - l} чисел меньше опорного {pivot}. Его место: позиция {l} + {p - l} = {p}. Меняем позиции {r} и {p}.") with { Call = call };
            c.Mark(r, StepMark.AtLeastPivot);
        }
        c.Mark(p, StepMark.Sorted);
        bool hasLeft = l < p;
        bool hasRight = p < r;
        string right = p + 1 == r ? $"позиция {r}" : $"позиции {p + 1}–{r}";
        string next = hasLeft ? "Сначала сортируем часть слева от опорного." : "Слева пусто. Переходим к правой части.";
        yield return c.S(SortOpType.MarkSorted, p, -1, $"Опорное {c.A[p]} встало на позицию {p}: в этом участке перед ним {p - l} меньших чисел, после него {r - p} чисел больше или равны ему. {next}")
            with { Call = call with { Detail = next } };
        c.MarkRange(l, p - 1, StepMark.Normal);
        c.MarkRange(p + 1, r, StepMark.Normal);

        if (hasLeft)
        {
            string afterLeft = hasRight ? $"После левой части → {right}." : "Правая часть пуста. После возврата участок будет готов.";
            foreach (var s in Quick(c, l, p - 1, call with { Detail = afterLeft })) yield return s;
            if (hasRight)
                yield return c.S(SortOpType.Info, -1, -1, $"Левая часть готова. Возвращаемся к участку {l}–{r} и переходим к правой части.")
                    with { Call = call with { Status = "Продолжаем", Detail = $"Левая часть готова. Дальше → {right}." } };
        }

        if (hasRight)
            foreach (var s in Quick(c, p + 1, r, call with { Detail = "После правой части участок будет готов." })) yield return s;

        yield return c.S(SortOpType.Info, -1, -1, $"Снова на участке {l}–{r}. Он готов. {(caller is null ? "Завершаем сортировку." : "Возвращаемся к предыдущему участку.")}")
            with { Call = call with { Status = "Продолжаем", Detail = "Работа на этом участке закончена." } };
    }
}
