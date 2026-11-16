using SortLab.Models;

namespace SortLab.Algorithms.Internal;

/// <summary>Сортировка вставками: каждый новый элемент «втыкается» в уже упорядоченную часть.</summary>
public sealed class InsertionSort : ISortAlgorithm
{
    public string Name => "Вставками (Insertion Sort)";
    public string Description => "Квадратичный алгоритм: берём число и вставляем его на нужное место в отсортированной части слева.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        yield return c.S(SortOpType.Info, -1, -1, "Идём слева направо: слева от «пальца» держим уже упорядоченные числа.");
        for (int i = 1; i < c.N; i++)
        {
            int key = c.A[i];
            c.Mark(i, StepMark.Swap);
            yield return c.S(SortOpType.Assign, i, -1, $"Запоминаем число {key}. Будем искать ему место среди чисел слева.");
            int j = i - 1;
            while (j >= 0)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, i, $"Сравниваем {c.A[j]} и {key}: {c.A[j]} больше {key}?") with { SavedKey = key };
                if (c.A[j] > key)
                {
                    c.A[j + 1] = c.A[j];
                    c.Mark(j, StepMark.Normal);
                    c.Mark(j + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Assign, j + 1, j, $"{c.A[j + 1]} больше {key} — сдвигаем {c.A[j + 1]} на одну клетку вправо.");
                    j--;
                }
                else
                {
                    yield return c.S(SortOpType.Info, j, -1, $"{key} не меньше {c.A[j]} — значит, место найдено сразу справа от {c.A[j]}.");
                    break;
                }
            }
            c.A[j + 1] = key;
            c.MarkRange(0, i, StepMark.Sorted);
            yield return c.S(SortOpType.Assign, j + 1, -1, $"Вставляем {key} на позицию {j + 1}. Числа слева снова по порядку!");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Сортировка Шелла: сравниваем числа на уменьшающемся расстоянии.</summary>
public sealed class ShellSort : ISortAlgorithm
{
    public string Name => "Шелла (Shell Sort)";
    public string Description => "Улучшение вставок: сначала сравниваем далёкие числа (большой шаг), потом всё ближе.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        int gap = c.N / 2;
        while (gap > 0)
        {
            yield return c.S(SortOpType.Info, -1, -1, $"Меняем правило: теперь сравниваем числа, стоящие через {gap} клеток друг от друга.");
            for (int i = gap; i < c.N; i++)
            {
                int key = c.A[i];
                int j = i;
                while (j >= gap)
                {
                    c.Mark(j - gap, StepMark.Compare);
                    c.Mark(j, StepMark.Swap);
                    yield return c.S(SortOpType.Compare, j - gap, j, $"Сравниваем {c.A[j - gap]} и {key}: они стоят через {gap} клеток.") with { SavedKey = key };
                    if (c.A[j - gap] > key)
                    {
                        c.A[j] = c.A[j - gap];
                        c.Mark(j - gap, StepMark.Normal);
                        c.Mark(j, StepMark.Swap);
                        yield return c.S(SortOpType.Assign, j, j - gap, $"{c.A[j]} больше {key} — сдвигаем {c.A[j]} на {gap} клеток вправо.");
                        j -= gap;
                    }
                    else
                    {
                        c.Mark(j - gap, StepMark.Normal);
                        break;
                    }
                }
                c.A[j] = key;
                c.Mark(j, StepMark.Normal);
                yield return c.S(SortOpType.Assign, j, -1, $"Кладём {key} на позицию {j}.");
            }
            gap /= 2;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Шаг стал равен 1, все числа по порядку.");
    }
}

/// <summary>Сортировка выбором: находим минимум и ставим его в начало.</summary>
public sealed class SelectionSort : ISortAlgorithm
{
    public string Name => "Выбором (Selection Sort)";
    public string Description => "Квадратичный алгоритм: находим самое маленькое из оставшихся чисел и ставим его в конец отсортированной части.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        for (int i = 0; i < c.N - 1; i++)
        {
            int min = i;
            c.Mark(min, StepMark.Pivot);
            yield return c.S(SortOpType.Info, i, -1, $"Ищем самое маленькое число на участке [{i}..{c.N - 1}]. Пока кандидат — {c.A[min]} (жёлто-фиолетовая подсветка).");
            for (int j = i + 1; j < c.N; j++)
            {
                c.Mark(j, StepMark.Compare);
                yield return c.S(SortOpType.Compare, j, min, $"Сравниваем {c.A[j]} и {c.A[min]}: какое меньше?");
                if (c.A[j] < c.A[min])
                {
                    c.Mark(min, StepMark.Normal);
                    min = j;
                    c.Mark(min, StepMark.Pivot);
                    yield return c.S(SortOpType.Info, j, -1, $"{c.A[j]} меньше — теперь кандидат на самое маленькое.");
                }
                else
                {
                    c.Mark(j, StepMark.Normal);
                }
            }
            if (min != i)
            {
                c.Swap(i, min);
                c.Mark(i, StepMark.Swap);
                c.Mark(min, StepMark.Swap);
                yield return c.S(SortOpType.Swap, i, min, $"Ставим самое маленькое число на позицию {i}: меняем местами {c.A[i]} и {c.A[min]}.");
                c.Mark(min, StepMark.Normal);
            }
            c.Mark(i, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, i, -1, $"Позиция {i} занята: {c.A[i]} — самое маленькое из оставшихся.");
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}

/// <summary>Улучшенная сортировка пузырьком: после прохода смотрим только до последнего обмена.</summary>
public sealed class ImprovedBubbleSort : ISortAlgorithm
{
    public string Name => "Пузырьком, улучшенный (Bubble Sort)";
    public string Description => "Соседние числа меняем местами, если они перепутаны; хвост после последнего обмена сразу считаем готовым.";

    public IEnumerable<SortStep> GetSteps(int[] input) => Run(new SortCtx(input));

    private static IEnumerable<SortStep> Run(SortCtx c)
    {
        c.MarkAll(StepMark.Normal);
        int last = c.N - 1;
        while (last > 0)
        {
            int newLast = -1;
            for (int i = 0; i < last; i++)
            {
                c.Mark(i, StepMark.Compare);
                c.Mark(i + 1, StepMark.Compare);
                yield return c.S(SortOpType.Compare, i, i + 1, $"Сравниваем соседей {c.A[i]} и {c.A[i + 1]}: кто больше?");
                if (c.A[i] > c.A[i + 1])
                {
                    c.Swap(i, i + 1);
                    c.Mark(i, StepMark.Swap);
                    c.Mark(i + 1, StepMark.Swap);
                    yield return c.S(SortOpType.Swap, i, i + 1, $"{c.A[i + 1]} больше {c.A[i]} — меняем соседей местами.");
                    newLast = i;
                }
                c.Mark(i, StepMark.Normal);
                c.Mark(i + 1, StepMark.Normal);
            }
            if (newLast < 0)
            {
                yield return c.S(SortOpType.Info, -1, -1, "Ни одного обмена не случилось — значит, всё уже по порядку!");
                break;
            }
            // Всё правее последнего обмена уже точно на своих местах.
            c.MarkRange(newLast + 1, c.N - 1, StepMark.Sorted);
            yield return c.S(SortOpType.MarkSorted, newLast + 1, -1, $"После позиции {newLast} обменов больше не было — эта часть уже отсортирована.");
            last = newLast;
        }
        c.MarkAll(StepMark.Sorted);
        yield return c.S(SortOpType.Info, -1, -1, "Готово! Все числа выстроены по возрастанию.");
    }
}
