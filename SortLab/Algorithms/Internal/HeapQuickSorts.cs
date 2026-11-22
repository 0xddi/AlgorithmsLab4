using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Пирамидальная сортировка: строим «пирамиду» (макс-кучу) и вынимаем максимум.</summary>
public sealed class HeapSort : ISortAlgorithm
{
    public string Name => "Пирамидальная (Heap Sort)";
    public string Reference =>
        """
        Макс-куча — каждый родитель не меньше своих детей. При просеивании выбираем большего ребёнка; при равенстве — левого. Меняем его с родителем, только если ребёнок больше.
        """;
    public string Description =>
        """
        Представьте массив как дерево: первая ячейка — корень. Если считать позиции от 1, у родителя на позиции p дети находятся на 2p и 2p + 1, пока эти позиции входят в кучу. Чтобы найти родителя, делим позицию ребёнка на 2 и отбрасываем остаток. Роли определяет место, а значения нужны для сравнения.

        В [4, 9, 6, 2, 7] дети числа 4 — числа 9 и 6. У числа 9 тоже есть дети: 2 и 7. В макс-куче каждый родитель не меньше своих детей. Чтобы исправить вершину, выбираем большего ребёнка: 9 поднимается вместо 4. Затем 4 сравниваем с 2 и 7 и опускаем ещё раз. Это просеивание вниз.

        Строим кучу снизу вверх. Максимум оказывается в корне. Меняем его с последним числом кучи, исключаем эту позицию и снова просеиваем корень. Так справа растёт готовая часть массива.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input)
    {
        var c = new SortCtx(input);
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, c.N >= 2
            ? $"Последний элемент стоит на позиции {c.N}. Позиция его родителя равна целой части ({c.N} / 2), то есть {c.N / 2}. Начнём с этого родителя и пройдём к корню."
            : "В массиве нет пары для сравнения.", new HeapFrame(c.N, c.N >= 2 ? c.N / 2 : null));

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
            yield return c.S(SortOpType.Swap, 0, end, $"Максимум {c.A[end]} занял позицию {end + 1}. В куче осталось {end} элементов.", new HeapFrame(end));
            c.Mark(end, StepMark.Sorted);
            if (end - 1 > 0)
                foreach (var s in SiftDown(c, 0, end - 1))
                    yield return s;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Каждый раз забирали максимум — числа выстроены по возрастанию.", new HeapFrame(0));
    }

    /// <summary>Просеивание: родитель опускается вниз, пока дети больше него.</summary>
    private static IEnumerable<SortStep> SiftDown(SortCtx c, int root, int end)
    {
        while (true)
        {
            int child = 2 * root + 1;
            int p = root + 1; // На диаграмме позиции нумеруются с единицы.
            var frame = new HeapFrame(end + 1, p);
            c.Mark(root, StepMark.HeapParent);
            if (child > end)
            {
                yield return c.S(SortOpType.Info, root, -1,
                    $"От позиции {p} ищем левого ребёнка: 2 × {p} = {frame.Left}. Это больше {frame.Size}, поэтому у числа {c.A[root]} нет детей.", frame);
                c.Mark(root, StepMark.Normal);
                yield break;
            }

            c.Mark(child, StepMark.HeapChild);
            bool hasRight = child + 1 <= end;
            if (hasRight) c.Mark(child + 1, StepMark.HeapChild);
            string right = hasRight ? $" (число {c.A[child + 1]})" : $" > {frame.Size}, поэтому правого ребёнка нет";
            yield return c.S(SortOpType.Info, root, child,
                $"Родитель — число {c.A[root]} на позиции {p}. Левый ребёнок: 2 × {p} = {frame.Left} (число {c.A[child]}). Правый ребёнок: 2 × {p} + 1 = {frame.Right}{right}.", frame);
            if (hasRight)
            {
                yield return c.S(SortOpType.Compare, child, child + 1,
                    $"Левый ребёнок — число {c.A[child]} на позиции {child + 1}. Правый ребёнок — число {c.A[child + 1]} на позиции {child + 2}. {(c.A[child] == c.A[child + 1] ? "Значения равны, выбираем левого ребёнка." : c.A[child] > c.A[child + 1] ? "Значение левого ребёнка больше, выбираем его." : "Значение правого ребёнка больше, выбираем его.")}", frame);
                c.Mark(child, StepMark.Normal);
                c.Mark(child + 1, StepMark.Normal);
                if (c.A[child + 1] > c.A[child]) child++;
            }
            c.Mark(child, StepMark.HeapChild);
            string childRole = child + 1 == frame.Left ? "левого ребёнка" : "правого ребёнка";
            yield return c.S(SortOpType.Compare, root, child,
                $"Для {childRole} на позиции {child + 1} берём целую часть ({child + 1} / 2) и получаем позицию родителя {p}. Сравниваем их значения: {c.A[child]} и {c.A[root]}.", frame);
            if (c.A[child] > c.A[root])
            {
                c.Swap(root, child);
                c.Mark(root, StepMark.Swap);
                c.Mark(child, StepMark.Swap);
                yield return c.S(SortOpType.Swap, root, child, $"Значение {childRole} ({c.A[root]}) больше значения родителя ({c.A[child]}). Меняем позиции {p} и {child + 1}. Продолжаем с позиции {child + 1}.", frame);
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
        Опорное число — последнее на текущем участке. Граница отделяет числа строго меньше опорного от остальных.

        Место опорного = начало участка + количество меньших чисел. Равные остаются в группе «больше или равно». Оба оставшихся участка сортируются отдельно.
        """;
    public string Description =>
        """
        Опорное число — значение, с которым сравниваем весь выбранный участок. Здесь берём последнее число. Просматриваем остальные слева направо: каждое строго меньшее переносим к левой границе. Равные опорному и большие остаются в другой группе.

        В [6, 2, 4] опорное — 4. Число 2 переходит влево, затем ставим 4 между группами: [2, 4, 6]. Позиция 4 уже окончательная. Повторяем то же правило отдельно слева и справа — это рекурсия. Участок длиной 0 или 1 уже готов; части могут быть разной длины.
        """;

    public IEnumerable<SortStep> GetSteps(int[] input)
    {
        var c = new SortCtx(input);
        c.MarkAll(StepMark.Normal);
        foreach (var s in Quick(c, 0, c.N - 1))
            yield return s;
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Массив разобран на маленькие кусочки и собран обратно по порядку.");
    }

    private static IEnumerable<SortStep> Quick(SortCtx c, int l, int r, RangeFrame? caller = null)
    {
        if (l > r) yield break;
        var call = new RangeFrame(l + 1, r + 1, caller);
        if (l == r)
        {
            c.Mark(l, StepMark.Sorted);
            yield return c.S(SortOpType.Info, l, -1, $"На позиции {l + 1} остался один элемент. Возвращаемся из вызова.")
                with { Call = call with { Detail = $"Здесь только число {c.A[l]}. Участок готов." } };
            yield break;
        }

        int pivot = c.A[r];
        call = call with { Detail = $"Разделяем участок вокруг опорного числа {pivot}." };
        c.Mark(r, StepMark.Pivot);
        yield return c.S(SortOpType.Info, r, -1, $"Берём позиции с {l + 1} по {r + 1}. Опорное число — последнее на этом участке: {pivot}. В начале участка собираем числа строго меньше опорного.") with { Call = call };

        int i = l - 1;
        for (int j = l; j < r; j++)
        {
            c.Mark(j, StepMark.Compare);
            yield return c.S(SortOpType.Compare, j, r, $"Число {c.A[j]} на позиции {j + 1} сравниваем с опорным {pivot}. {(c.A[j] < pivot ? "Оно меньше, поэтому расширяем группу меньших чисел." : "Оно больше или равно опорному, поэтому граница группы меньших чисел остаётся на месте.")}") with { Call = call };
            if (c.A[j] < pivot)
            {
                i++;
                if (i != j)
                {
                    c.Swap(i, j);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, j, $"{c.A[i]} меньше опорного — переносим его с позиции {j + 1} на позицию {i + 1}, следующую в группе меньших чисел.") with { Call = call };
                    c.Mark(i, StepMark.Normal);
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
            yield return c.S(SortOpType.Swap, p, r, $"В участке {p - l} чисел меньше опорного {pivot}. Его место — позиция {l + 1} + {p - l} = {p + 1}. Меняем позиции {r + 1} и {p + 1}.") with { Call = call };
        }
        c.Mark(r, StepMark.Normal);
        c.Mark(p, StepMark.Sorted);
        bool hasLeft = l < p;
        bool hasRight = p < r;
        string right = p + 1 == r ? $"позиция {r + 1}" : $"позиции {p + 2}–{r + 1}";
        string next = hasLeft ? "Сначала сортируем часть слева от опорного." : "Слева пусто. Переходим к правой части.";
        yield return c.S(SortOpType.MarkSorted, p, -1, $"Опорное {c.A[p]} встало на позицию {p + 1}: в этом участке перед ним {p - l} меньших чисел, после него {r - p} чисел больше или равны ему. {next}")
            with { Call = call with { Detail = next } };

        if (hasLeft)
        {
            string afterLeft = hasRight ? $"После левой части → {right}." : "Правая часть пуста. После возврата участок будет готов.";
            foreach (var s in Quick(c, l, p - 1, call with { Detail = afterLeft })) yield return s;
            if (hasRight)
                yield return c.S(SortOpType.Info, -1, -1, $"Левая часть готова. Возвращаемся к участку {l + 1}–{r + 1} и переходим к правой части.")
                    with { Call = call with { Status = "Продолжаем", Detail = $"Левая часть готова. Дальше → {right}." } };
        }

        if (hasRight)
            foreach (var s in Quick(c, p + 1, r, call with { Detail = "После правой части участок будет готов." })) yield return s;

        yield return c.S(SortOpType.Info, -1, -1, $"Снова на участке {l + 1}–{r + 1}. Он готов. {(caller is null ? "Завершаем сортировку." : "Возвращаемся к предыдущему участку.")}")
            with { Call = call with { Status = "Продолжаем", Detail = "Работа на этом участке закончена." } };
    }
}
